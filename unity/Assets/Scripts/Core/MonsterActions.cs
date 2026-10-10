namespace NightShift.Core
{
    /// <summary>
    /// The monster's action space: one discrete branch of size 6 where the action is the index of
    /// the target room. Choosing the current room means "stay"; choosing the Office (5) from a
    /// corridor means "try to enter". See docs/ml-design.md, section 4.
    /// </summary>
    public static class MonsterActions
    {
        /// <summary>Size of the discrete branch.</summary>
        public const int Count = RoomGraph.RoomCount;

        /// <summary>Action that tries to enter the office (only legal from a corridor).</summary>
        public const int EnterOffice = (int)RoomId.Office;

        /// <summary>The action that keeps the monster where it is.</summary>
        public static int Stay(RoomId current) => (int)current;

        /// <summary>
        /// True if <paramref name="action"/> can be chosen from <paramref name="current"/>:
        /// the current room, a connected room, or the Office from a corridor.
        /// The door state does not affect legality: hitting a closed door is a legal (and
        /// penalized) choice, because learning to avoid it is part of the task.
        /// </summary>
        public static bool IsLegal(RoomId current, int action)
        {
            if (action < 0 || action >= Count || current == RoomId.Office)
            {
                return false;
            }

            RoomId target = (RoomId)action;
            return target == current || RoomGraph.AreConnected(current, target);
        }

        /// <summary>Writes the legal actions of <paramref name="current"/> into <paramref name="mask"/>.</summary>
        /// <param name="mask">Array of length <see cref="Count"/>; true means the action is allowed.</param>
        public static void FillLegalMask(RoomId current, bool[] mask)
        {
            for (int action = 0; action < Count; action++)
            {
                mask[action] = IsLegal(current, action);
            }
        }
    }
}
