using System;
using System.Collections.Generic;
using NightShift.Core;

namespace NightShift.Monster
{
    /// <summary>
    /// Baseline: always moves one step along the shortest path to the office and tries to enter
    /// as soon as it reaches a corridor. Ignores cameras and doors. This is the "obvious" monster
    /// the trained agent has to beat.
    /// </summary>
    public class ScriptedMonsterPolicy : IMonsterPolicy
    {
        private readonly Random random;
        private readonly List<RoomId> bestNeighbors = new List<RoomId>(RoomGraph.RoomCount);

        /// <param name="seed">Used to break ties between equally short routes.</param>
        public ScriptedMonsterPolicy(int seed)
        {
            random = new Random(seed);
        }

        public string PolicyName => "scripted";

        public void OnNightStarted(GameState state)
        {
        }

        public void Decide(GameState state)
        {
            state.ApplyMonsterAction(ChooseAction(state));
        }

        public void OnNightEnded(GameState state, NightOutcome outcome)
        {
        }

        /// <summary>Enter from a corridor; otherwise go to the neighbor closest to the office.</summary>
        public int ChooseAction(GameState state)
        {
            RoomId room = state.MonsterRoom;
            if (RoomGraph.IsDoorRoom(room))
            {
                return MonsterActions.EnterOffice;
            }

            int bestDistance = int.MaxValue;
            bestNeighbors.Clear();
            foreach (RoomId neighbor in RoomGraph.GetNeighbors(room))
            {
                int distance = RoomGraph.DistanceToOffice(neighbor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNeighbors.Clear();
                }
                if (distance == bestDistance)
                {
                    bestNeighbors.Add(neighbor);
                }
            }
            return (int)bestNeighbors[random.Next(bestNeighbors.Count)];
        }
    }
}
