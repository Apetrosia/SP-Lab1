using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        string[] prefixes = { "http://localhost:8080/" };
        string rootDirectory = args.Length > 0 ? args[0] : null;
        SimpleListenerExample(prefixes, rootDirectory);
    }

    static void SimpleListenerExample(string[] prefixes, string rootDirectory = null)
    {
        if (!HttpListener.IsSupported)
        {
            Console.WriteLine("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
            return;
        }
        if (prefixes == null || prefixes.Length == 0)
            throw new ArgumentException("prefixes");

        rootDirectory = rootDirectory ?? Directory.GetCurrentDirectory();
        string logFile = "requests.log";
        object logLock = new object();

        HttpListener listener = new HttpListener();
        foreach (string s in prefixes)
            listener.Prefixes.Add(s);

        listener.Start();
        Console.WriteLine($"Listening... (root: {rootDirectory})");

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            Task.Run(() => ProcessRequest(context, rootDirectory, logFile, logLock));
        }
    }

    static void ProcessRequest(HttpListenerContext context, string rootDirectory, string logFile, object logLock)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        int statusCode = 200;
        try
        {
            string path = request.Url.AbsolutePath.TrimStart('/');
            string fullPath = Path.GetFullPath(Path.Combine(rootDirectory, path));

            if (!fullPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 404;
                response.StatusDescription = "Not Found";
                WriteErrorResponse(response, "404 - Not Found");
                statusCode = 404;
            }
            else if (File.Exists(fullPath))
            {
                ServeFile(response, fullPath);
                statusCode = 200;
            }
            else
            {
                response.StatusCode = 404;
                response.StatusDescription = "Not Found";
                WriteErrorResponse(response, "404 - Not Found");
                statusCode = 404;
            }
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            response.StatusDescription = "Internal Server Error";
            WriteErrorResponse(response, "500 - Internal Server Error");
            statusCode = 500;
            Console.WriteLine($"Error: {ex}");
        }
        finally
        {
            LogRequest(request, statusCode, logFile, logLock);
            response.OutputStream.Close();
        }
    }

    static void ServeFile(HttpListenerResponse response, string filePath)
    {
        string extension = Path.GetExtension(filePath);
        string mime = extension switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
        response.ContentType = mime;

        byte[] buffer = File.ReadAllBytes(filePath);
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    static void WriteErrorResponse(HttpListenerResponse response, string message)
    {
        byte[] buffer = Encoding.UTF8.GetBytes($"<html><body><h1>{message}</h1></body></html>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    static void LogRequest(HttpListenerRequest request, int statusCode, string logFile, object logLock)
    {
        string ip = request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
        string path = request.Url.AbsolutePath;
        string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logLine = $"{date} | IP: {ip} | Path: {path} | Code: {statusCode}";

        lock (logLock)
        {
            File.AppendAllLines(logFile, new[] { logLine });
        }
        Console.WriteLine(logLine);
    }
}