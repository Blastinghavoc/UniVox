using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.Common;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// A fixed capacity run length encoded array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RLEArray<T> : IEnumerable<T> where T : IEquatable<T>
    {
        public struct Run
        {
            public StartEndRange range;
            public T value;
        }

        public bool IsEmpty
        {
            get
            {
                return runs.Count == 1 && runs[0].value.Equals(default);
            }
        }

        public int NumRuns { get => runs.Count; }
        public int Capacity { get; private set; }

        private List<Run> runs = new List<Run>();

        //Empty constructor
        public RLEArray()
        {

        }

        public RLEArray(Vector3Int dimensions)
        {
            InitialiseEmpty(dimensions);
        }

        public RLEArray(Vector3Int dimensions, T[] initialValues)
        {
            InitialiseWithData(dimensions, initialValues);
        }

        public virtual void InitialiseEmpty(Vector3Int dimensions)
        {
            var capacity = dimensions.x * dimensions.y * dimensions.z;
            //Initially a single empty/default run
            runs.Add(new Run() { range = new StartEndRange() { end = capacity }, value = default });
            Capacity = capacity;
        }

        public virtual void InitialiseWithData(Vector3Int dimensions, T[] initialData)
        {
            Profiler.BeginSample("RLE From Array");
            Capacity = initialData.Length;
            Assert.AreEqual(dimensions.x * dimensions.y * dimensions.z, Capacity, "Length of initial data must equal product of dimensions");

            //Initialise from array
            T currentRunValue = default;
            StartEndRange range = new StartEndRange(0, 0);
            for (int i = 0; i < initialData.Length; i++)
            {
                var value = initialData[i];

                if (!value.Equals(currentRunValue))
                {
                    range.end = i;
                    if (range.Length > 0)
                    {
                        runs.Add(new Run() { range = range, value = currentRunValue });
                    }
                    range.start = i;
                    currentRunValue = value;
                }
            }
            //Add the last run
            range.end = Capacity;
            if (range.Length > 0)
            {
                runs.Add(new Run() { range = range, value = currentRunValue });
            }
            Profiler.EndSample();
        }

        public T[] ToArray()
        {
            Profiler.BeginSample("RLE ToArray");

            T[] array = new T[Capacity];
            int index = 0;

            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                for (int j = 0; j < run.range.Length; j++)
                {
                    array[index++] = run.value;
                }
            }
            Profiler.EndSample();
            return array;
        }

        public T Get(int uncompressedIndex)
        {
            BSearch(uncompressedIndex, out var runIndex);
            return runs[runIndex].value;
        }

        public void Set(int uncompressedIndex, T value)
        {
            BSearch(uncompressedIndex, out var runIndex);
            var run = runs[runIndex];
            if (run.value.Equals(value))
            {
                return;//Nothing needs changing
            }
            else
            {
                //Check if index is the start or end of the run
                bool startOfRun = uncompressedIndex == run.range.start;
                bool endOfRun = uncompressedIndex == run.range.end - 1;
                int prevRunIndex = runIndex - 1;
                int nextRunIndex = runIndex + 1;

                if (startOfRun && endOfRun)
                {
                    if (TryGetRun(prevRunIndex, out var prevRun) && prevRun.value.Equals(value) &&
                        TryGetRun(nextRunIndex, out var nextRun) && nextRun.value.Equals(value))
                    {//Can merge the adjacent runs
                        prevRun.range.end = nextRun.range.end;
                        runs[prevRunIndex] = prevRun;

                        runs.RemoveRange(runIndex, 2);//Remove this run and the next one, as they have been merged
                        return;//Done
                    }
                }
                else if (startOfRun)
                {
                    if (TryGetRun(prevRunIndex, out var prevRun) && prevRun.value.Equals(value))
                    {
                        //Can just increase the length of the previous run
                        prevRun.range.end++;
                        runs[prevRunIndex] = prevRun;
                        run.range.start++;
                        runs[runIndex] = run;
                        return;//Done
                    }
                }
                else if (endOfRun)
                {
                    if (TryGetRun(nextRunIndex, out var nextRun) && nextRun.value.Equals(value))
                    {
                        //Can just increase the length of the next run
                        nextRun.range.start--;
                        runs[nextRunIndex] = nextRun;
                        run.range.end--;
                        runs[runIndex] = run;
                        return;//Done
                    }
                }

                //Have to split the run
                Run prev = new Run() { range = new StartEndRange(run.range.start, uncompressedIndex), value = run.value };
                Run newRun = new Run() { range = new StartEndRange(uncompressedIndex, uncompressedIndex + 1), value = value };
                Run post = new Run() { range = new StartEndRange(uncompressedIndex + 1, run.range.end) };
                if (prev.range.Length > 0)
                {
                    runs[runIndex++] = prev;

                    runs.Insert(runIndex++, newRun);
                }
                else
                {
                    runs[runIndex++] = newRun;
                }

                if (post.range.Length > 0)
                {
                    runs.Insert(runIndex, post);
                }
            }
        }

        private bool TryGetRun(int runIndex, out Run run)
        {
            if (runIndex >= 0 && runIndex < runs.Count)
            {
                run = runs[runIndex];
                return true;
            }
            run = default;
            return false;
        }

        private void BSearch(int uncompressedIndex, out int runIndex)
        {
            if (!rangeContains(new StartEndRange(0, Capacity), uncompressedIndex))
            {
                throw new IndexOutOfRangeException($"Index {uncompressedIndex} is not contained in the array");
            }

            StartEndRange runRange = new StartEndRange(0, runs.Count);
            runIndex = runRange.Length / 2;
            StartEndRange currentRange = runs[runIndex].range;
            while (!rangeContains(currentRange, uncompressedIndex, out bool left))
            {
                if (left)
                {
                    runRange = new StartEndRange(runRange.start, runIndex);
                }
                else
                {
                    runRange = new StartEndRange(runIndex + 1, runRange.end);
                }
                int prevRunIndex = runIndex;
                runIndex = runRange.Length / 2 + runRange.start;

                if (prevRunIndex == runIndex)
                {
                    throw new Exception("Bsearch did not terminate");
                }

                currentRange = runs[runIndex].range;
            }

        }

        /// <summary>
        /// The left out parameter indicates which direction the index is out of range
        /// if the method returns false. If it returns true, left is always true.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="index"></param>
        /// <param name="left"></param>
        /// <returns></returns>
        private bool rangeContains(StartEndRange range, int index, out bool left)
        {
            bool valid = true;
            left = true;
            if (index < range.start)
            {
                valid = false;
            }
            if (index >= range.end)
            {
                left = false;
                valid = false;
            }
            return valid;
        }

        private bool rangeContains(StartEndRange range, int index)
        {
            return index >= range.start && index < range.end;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                for (int j = 0; j < run.range.Length; j++)
                {
                    yield return run.value;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}