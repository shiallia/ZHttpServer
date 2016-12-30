using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace illidan
{
    public class Processor
    {
        public System.Net.Sockets.TcpClient socket;
        public Server srv;

        private System.IO.Stream inputByteStream;
        public System.IO.StreamWriter outputStream;
        public System.IO.BufferedStream outputByteStream;

        public String http_method;
        public String http_url;
        public String http_url_ondisk;
        public String http_protocol_versionstring;
        public System.Collections.Hashtable httpHeaders = new Hashtable();


        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public Processor(System.Net.Sockets.TcpClient s, Server srv)
        {
            this.socket = s;
            this.srv = srv;
            
        }


        private string streamReadLine(System.IO.Stream inputStream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }
        public void process()
        {
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            inputByteStream = new System.IO.BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new System.IO.StreamWriter(new System.IO.BufferedStream(socket.GetStream()));
            outputByteStream = new System.IO.BufferedStream(socket.GetStream());
            try
            {
                parseRequest();
                if (http_method.Equals("GET"))
                {
                    handleGETRequest();
                }
                else if (http_method.Equals("POST"))
                {
                    handlePOSTRequest();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.ToString());
                writeFailure();
            }
            //outputStream.Flush();
            // bs.Flush(); // flush any remaining output
            inputByteStream = null; outputStream = null; // bs = null;            
            socket.Close();
        }

        public void parseRequest()
        {
            String request = streamReadLine(inputByteStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                
                throw new Exception("invalid http request line");
               
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_url_ondisk = srv.rootdic + System.Web.HttpUtility.UrlDecode(http_url, Encoding.GetEncoding("utf-8"));
            http_protocol_versionstring = tokens[2];

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("starting: " + request);
            Console.ResetColor();

            Console.WriteLine("readHeaders()");
            String line;
            while ((line = streamReadLine(inputByteStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                httpHeaders[name] = value;
            }
        }
       

        public void handleGETRequest()
        {
                       
            if (FindType(http_url_ondisk) == RequestType.folder)
            {
                writeFolderDetail();

            }
            if (FindType(http_url_ondisk) == RequestType.normalfile)
            {
                putFile();
                
            }
            if (FindType(http_url_ondisk) == RequestType.web)
            {
                putWeb();

            }
            if (FindType(http_url_ondisk) == RequestType.readablefile)
            {
                writeReadableFile();
            }
            if (FindType(http_url_ondisk) == RequestType.error)
            {
                writeFailure();
            }
        }

        private const int BUF_SIZE = 4096;
        public void handlePOSTRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            int content_len = 0;
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            if (this.httpHeaders.ContainsKey("Content-Length"))
            {
                content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                if (content_len > MAX_POST_SIZE)
                {
                    throw new Exception(
                        String.Format("POST Content-Length({0}) too big for this simple server",
                          content_len));
                }
                byte[] buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    Console.WriteLine("starting Read, to_read={0}", to_read);

                    int numread = this.inputByteStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    Console.WriteLine("read finished, numread={0}", numread);
                    if (numread == 0)
                    {
                        if (to_read == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, System.IO.SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            //srv.handlePOSTRequest(this, new System.IO.StreamReader(ms));
        }
       

        public void writeFailure()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("请求未发现");
            Console.ResetColor();

            outputStream.WriteLine("HTTP/1.0 404 File not found");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.Flush();
        }

        public void writeFolderDetail()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("这个请求是一个目录");
            Console.ResetColor();


            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: text/html");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.Flush();

            DirectoryInfo folder = new DirectoryInfo(http_url_ondisk);

           

            outputStream.WriteLine("<!DOCTYPE html>");
            outputStream.WriteLine("<html><head><meta charset='UTF-8'/></head><body><h1>test server</h1>");
            
            foreach (DirectoryInfo directnory in folder.GetDirectories())
            {
                outputStream.WriteLine("<a href='{0}'>{0}</a><br/>", (directnory.Name) + "/");
            }

            foreach (FileInfo file in folder.GetFiles("*"))
            {
                outputStream.WriteLine("<a href='{0}'>{0}</a><br/>", file.Name);
            }
            outputStream.WriteLine("</body></html>");
            outputStream.Flush();

        }


        public void putFile()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("这个请求是一个普通文件");
            Console.ResetColor();

            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: application/octet-stream");
            outputStream.WriteLine("Content-Disposition: attachment");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.Flush();

            FileStream stream = new FileStream(http_url_ondisk, FileMode.Open, FileAccess.Read);
            try
            {
                byte[] buff2 = new byte[1024];
                int count = 0;
                while ((count = stream.Read(buff2, 0, 1024)) != 0)
                {
                    outputByteStream.Write(buff2, 0, count);
                }
                outputByteStream.Flush();                
                outputByteStream.Close();
                stream.Close();
            }
            catch (Exception)
            {
                Console.WriteLine("停止传输文件");
                stream.Close();
                outputByteStream.Close();
            }
        }

        public void putWeb()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("这个请求是一个web文件");
            Console.ResetColor();

            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: text/html");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.Flush();
            //using (FileStream fsRead = new FileStream(http_url_ondisk, FileMode.Open))
            //{
            //    int fsLen = (int)fsRead.Length;
            //    byte[] heByte = new byte[fsLen];
            //    int r = fsRead.Read(heByte, 0, heByte.Length);
            //    string myStr = System.Text.Encoding.UTF8.GetString(heByte);                
            //    outputStream.WriteLine(myStr);
            //    outputStream.Flush();
            //}
            FileStream stream = new FileStream(http_url_ondisk, FileMode.Open, FileAccess.Read);
            try
            {
                byte[] buff2 = new byte[1024];
                int count = 0;
                while ((count = stream.Read(buff2, 0, 1024)) != 0)
                {
                    outputByteStream.Write(buff2, 0, count);
                }
                outputByteStream.Flush();                
                outputByteStream.Close();
                stream.Close();
            }
            catch (Exception)
            {
                Console.WriteLine("停止传输文件");
                stream.Close();
                outputByteStream.Close();
            }
        }

        public void writeReadableFile()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("这个请求是一个图片文件");
            Console.ResetColor();

            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: application/octet-stream");
            //outputStream.WriteLine("Content-Type: Image");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
            outputStream.Flush();

            FileStream stream = new FileStream(http_url_ondisk, FileMode.Open, FileAccess.Read);
            try
            {
                byte[] buff2 = new byte[1024];
                int count = 0;
                while ((count = stream.Read(buff2, 0, 1024)) != 0)
                {
                    outputByteStream.Write(buff2, 0, count);
                }
                outputByteStream.Flush();
                //outputStream.Flush();
                outputByteStream.Close();
                stream.Close();
            }
            catch (Exception)
            {
                Console.WriteLine("停止传输文件");
                stream.Close();
                outputByteStream.Close();
            }
        }

        public RequestType FindType(string url)
        {
            

            if (File.Exists(url))
            {                
                if (url.EndsWith(".html"))
                {
                    return RequestType.web;
                }else if(url.EndsWith(".jpg"))
                {
                    return RequestType.readablefile;
                }
                else
                {
                    return RequestType.normalfile;
                }
            }
            else if (Directory.Exists(url))
            {
                return RequestType.folder;
            }
            else
            {
                return RequestType.error;
            }
        }
    }
}

public enum RequestType { web = 1, normalfile, readablefile,folder, error };