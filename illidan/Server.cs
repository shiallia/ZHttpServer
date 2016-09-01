using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace illidan
{
    public class Server
    {
        public int port;
        System.Net.Sockets.TcpListener listener;
        bool is_active = true;
        public string rootdic;

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
