using System.Net.Sockets;
using System.Net;
using System.Threading;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.IO;

namespace BackendTask2
{
	public enum State : byte { ready, queued, progress, eexist };

	class Server
	{
		private static void Send(TcpClient client, byte[] buffer, string text)
		{
			string message = "HTTP/1.1 200 OK\nContent-type: text/html\nContent-Length:"
				+ text.Length.ToString () + "\r\n\r\n" + text;
			buffer = Encoding.ASCII.GetBytes (message);
			client.GetStream ().Write (buffer, 0, buffer.Length);
		}

		public Server(int port)
		{
			TcpListener Listener = new TcpListener (IPAddress.Any, port);
			Listener.Start();

			TcpClient Client = Listener.AcceptTcpClient (); // ждать подключение единственного клиента

			while (true) {
				string text = "";
				int count;
				byte[] buffer = new byte[1024];
				while (true) {
					count = Client.GetStream ().Read (buffer, 0, buffer.Length);
					text += Encoding.ASCII.GetString (buffer, 0, count);
					if (text.IndexOf("\r\n\r\n") >=  0 || text.Length > 4096) {
						break;
					}
				} // ждать сообщение

				// parse
				string query = new string (text.Skip (6).TakeWhile (c => c != ' ').ToArray ());
				NameValueCollection qscoll = HttpUtility.ParseQueryString (query);

				if (qscoll.Keys [0] == "get") {
					int id = Convert.ToInt32 (qscoll ["get"]);
					SortState sortstate = Worker.GetState (id);
					lock (sortstate) {
						string data = sortstate.State == State.ready ? File.ReadAllText (sortstate.Filename) : null;
						string json = "{\r\n\t\"state\": \"" + sortstate.State + "\"\r\n\t\"data\": [" + data + "]\r\n}";
						Send (Client, buffer, json);
					}

				} else if (qscoll.Keys [0] == "concurrency" && qscoll.Keys [1] == "sort") {
					SortRequest res = new SortRequest ();
					res.N = Convert.ToInt32 (qscoll ["concurrency"]);
					res.Url = qscoll ["sort"];
					Worker.Add (res);
					Send (Client, buffer, res.ID.ToString ());
				} else {
					Send (Client, buffer, ""); // ответ на запрос иконки, и т.п.
				}
			}
		}

		// точка входа
		static void Main(string[] args)
		{
			new Worker ();
			new Server (8888); // циклит основной поток
		}
	}
}