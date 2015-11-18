using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using System.Web;
using System.Net;
using System.IO;

namespace BackendTask2
{
	public class Worker
	{
		public Worker ()
		{
			Thread Thread = new Thread (new ThreadStart (Loop));
			Thread.Start ();
		}

		private static Queue<SortRequest> _sort_queue = new Queue<SortRequest>(8);
		private static LinkedList<SortState> _sort_stats = new LinkedList<SortState>();
		private static int[] dop, data;

		public static void Add(SortRequest request)
		{
			lock (_sort_stats) {
				SortState sortstate = new SortState ();
				sortstate.ID = request.ID;
				sortstate.State = State.queued;
				_sort_stats.AddFirst (sortstate); // свежие записи -- в начало списка, старые остаются в конце
			}
			lock (_sort_queue) {
				_sort_queue.Enqueue (request);
			}
		}

		public static SortState GetState(int id)
		{
			lock (_sort_stats) {	
				SortState sortstate = _sort_stats.FirstOrDefault (v => v.ID == id);
				if (sortstate == null) {
					return new SortState () {
						State = State.eexist
					};
				} else {
					return sortstate.Copy ();
				}
			}
		}

		private static void Loop()
		{
			while (true) {
				if (_sort_queue.Count > 0) {
					// process next query
					SortRequest r;
					lock (_sort_queue) {
						r = _sort_queue.Dequeue ();
					}
					lock (_sort_stats) {	
						SortState sortstate = _sort_stats.First (v => v.ID == r.ID);
						sortstate.State = State.progress;
						sortstate.Filename = r.Filename ();
					}

					// load data to file
					using (WebClient client = new WebClient()) {
						client.DownloadFile(r.Url, r.Filename ());
					}

					// read file to ram
					data = File.ReadAllText (r.Filename ()).Split (',').Select (v => Convert.ToInt32 (v)).ToArray ();
					dop = new int[data.Length];

					// start sort threads
					Thread[] threads = new Thread[r.N];
					AutoResetEvent[] semaphores = new AutoResetEvent[r.N];
					for (int i = 0; i < r.N; i++) {
						semaphores [i] = new AutoResetEvent (false);
						threads [i] = new Thread (new ParameterizedThreadStart (Sort));
					}
					for (int i = 0; i < r.N; i++) {
						threads [i].Start (new object[] { semaphores, r, i });
					}

					// wait until sort compleate
					semaphores [0].WaitOne ();
					//write data
					using (StreamWriter sw = new StreamWriter (r.Filename ())) {
						sw.Write (data [0].ToString ());
						foreach (int v in data.Skip(1)) {
							sw.Write (", " + v.ToString ()); // format like json array
						}
					}

					lock (_sort_stats) {	
						SortState sortstate = _sort_stats.First (v => v.ID == r.ID);
						sortstate.State = State.ready;
						sortstate.Filename = r.Filename ();
					}

					for (int i = 0; i < r.N; i++) {
						semaphores [i].Dispose ();
					}
					// test убрать
					int inverse = 0;
					for (int i = 1; i < data.Length; i++) {
						if (data [i] < data [i - 1]) {
							inverse++;
							//Console.WriteLine ("data [{1}] = {0}", data [i], i);
							//Console.WriteLine ("data [{1}] = {0}", data [i - 1], i - 1);
						}
					}
					Console.WriteLine ("Inverse = {0}. ID = {1}.", inverse, r.ID);
				}

				// remove old stats and files
				lock (_sort_stats) {
					if (_sort_stats.Count > 0) {
						SortState r = _sort_stats.Last ();
						if (r.IsOld ()) {
							File.Delete (r.Filename);
							_sort_stats.RemoveLast ();
						}
					}
				}
			}
		}

		private static void Sort(object obj)
		{
			object[] objs = (object[])obj;
			AutoResetEvent[] semaphores = (AutoResetEvent[])objs [0];
			SortRequest r = (SortRequest)objs [1];
			int rn = r.N;
			int index = (int)objs [2];
			int dl = data.Length;

			int b, m, e;
			b = index * dl / rn;
			e = (1 + index) * dl / rn;
			Array.Sort (data, b, e - b);

			// merge sort results
			int j1, j2 = 1;
			while (b != 0 || e != dl) {
				j1 = j2;
				j2 = j1 * 2;

				if (index % j2 == j1) {
					break;
				}

				if (index % j2 == 0) {
					if (index + j1 < rn) {
						semaphores [index + j1].WaitOne ();
						m = e;
						e = Math.Min ((j2 + index) * dl / rn, dl);

						lock (data.SyncRoot) {
							lock (dop.SyncRoot) {
								Merge (b, m, e);
							}
						}
					}
				}
			}
			semaphores [index].Set ();
		}

		// алгоритм слияния
		private static void Merge(int start, int middle, int end)
		{
			int i1 = start;
			int i2 = middle;
			int j = start;
			while (i1 < middle && i2 < end) {
				dop [j++] = data [i1] < data [i2] ? data [i1++] : data [i2++];
			}
			while (i1 < middle) {
				dop [j++] = data [i1++];
			}
			while (i2 < end) {
				dop [j++] = data [i2++];
			}
			for (int i = 0; i < end; i++) {
				data [i] = dop [i];
			}
		}
	}
}

