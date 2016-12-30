using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;


namespace illidan
{
    public delegate void ApiEventHandler(string[] pars, TcpClient s);
    public class Server
    {
        public int port;
        System.Net.Sockets.TcpListener listener;
        bool is_active = true;
        public string rootdic;
        public Dictionary<String, ApiEventHandler> apidic;

        public void RegisterApi(string apiname , ApiEventHandler fun)
        {
            apidic.Add(apiname, fun);
        }

        public void RemoveApi(string apiname)
        {
            apidic.Remove(apiname);
        }

        public Server(int port, string rootdic)
        {
            this.port = port;
            this.rootdic = rootdic;
        }

        public void Start()
        {
            listener = new System.Net.Sockets.TcpListener(port);
            listener.Start();
            while (is_active)
            {
                System.Net.Sockets.TcpClient s = listener.AcceptTcpClient();
                Processor processor = new Processor(s, this);
                Thread thread = new Thread(new ThreadStart(processor.process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

       
        public void Stop()
        {
            listener.Stop();
            is_active = false;
        }

        
    }
    

    
}
