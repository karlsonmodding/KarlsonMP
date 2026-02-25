using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP.GamemodeApi
{
    public class KMP_TaskScheduler
    {
        public class ScheduledTask
        {
            public Action Task;
            public DateTime Time;
            public bool ran;
            public uint identifier;

            internal ScheduledTask(Action task, DateTime time)
            {
                Task = task;
                Time = time;
                ran = false;
                identifier = idt++;
            }
        }
        public static List<ScheduledTask> scheduledTasks = new List<ScheduledTask>();
        private static List<ScheduledTask> tasksToSchedule = new List<ScheduledTask>();
        private static uint idt = 0;

        public static uint Schedule(Action task, DateTime whenToRun)
        {
            var st = new ScheduledTask(task, whenToRun);
            tasksToSchedule.Add(st);
            return st.identifier;
        }

        public static void CancelTask(uint id)
        {
            scheduledTasks.RemoveAll(x => x.identifier == id);
            tasksToSchedule.RemoveAll(x => x.identifier == id);
        }

        public static void ClearAndAddTasks()
        {
            scheduledTasks.RemoveAll(x => x.ran);
            scheduledTasks.AddRange(tasksToSchedule);
            tasksToSchedule.Clear();
        }
    }
}
