using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class GestureCommandHistory
    {
        private readonly Queue<GestureCommand> commands = new Queue<GestureCommand>();
        private readonly float historySeconds;
        private readonly int maxCount;
        private GestureCommand lastRecorded = GestureCommand.None;

        public GestureCommandHistory(float historySeconds = 2f, int maxCount = 24)
        {
            this.historySeconds = Mathf.Max(0.1f, historySeconds);
            this.maxCount = Mathf.Max(1, maxCount);
        }

        public int Count => commands.Count;

        public void Record(GestureCommand command)
        {
            if (!command.IsValid || IsDuplicate(command))
            {
                Trim(Time.time);
                return;
            }

            commands.Enqueue(command);
            lastRecorded = command;
            Trim(command.TriggeredTime > 0f ? command.TriggeredTime : Time.time);
        }

        public void Clear()
        {
            commands.Clear();
            lastRecorded = GestureCommand.None;
        }

        public GestureCommand[] Snapshot()
        {
            Trim(Time.time);
            return commands.ToArray();
        }

        private bool IsDuplicate(GestureCommand command)
        {
            if (!lastRecorded.IsValid || lastRecorded.Kind != command.Kind)
            {
                return false;
            }

            if (command.Kind == GestureCommandKind.StaticPose)
            {
                return lastRecorded.StaticGesture == command.StaticGesture;
            }

            return lastRecorded.MotionGesture == command.MotionGesture
                && Mathf.Approximately(lastRecorded.TriggeredTime, command.TriggeredTime);
        }

        private void Trim(float now)
        {
            while (commands.Count > maxCount)
            {
                commands.Dequeue();
            }

            while (commands.Count > 0)
            {
                var oldest = commands.Peek();
                if (now - oldest.TriggeredTime <= historySeconds)
                {
                    break;
                }

                commands.Dequeue();
            }
        }
    }
}
