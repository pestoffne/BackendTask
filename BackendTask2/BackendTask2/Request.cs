using System;
using System.Collections.Specialized;
using System.Web;
using System.Linq;

namespace BackendTask2
{
	public class SortRequest
	{
		public int ID { get; private set; }
		public int N { get; set; }
		public string Url { get; set; }

		private static int _sequence = 0;

		public SortRequest()
		{
			ID = _sequence++;
		}

		public string Filename()
		{
			return ID + ".txt";
		}
	}

	public class SortState
	{
		public int ID { get; set; }
		public State State { get; set; }
		public string Filename { get; set; }

		public SortState Copy()
		{
			return new SortState () {
				ID = this.ID,
				State = this.State,
				Filename = this.Filename
			};
		}

		private int start_tick;

		public SortState()
		{
			start_tick = Environment.TickCount;
		}

		public bool IsOld()
		{
			return Environment.TickCount - start_tick > 10 * 60 * 1000; // запись создана более 10 минут назад
		}
	}
}

