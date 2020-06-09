using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    public class PipelineStage
    {
        public string Name { get; private set; }
        public int Order { get; private set; }

        /// <summary>
        /// The condition to be evalutated to decide whether to move the chunk to the next stage,
        /// or terminate it here. true -> continue, false -> terminate
        /// </summary>
        public Func<Vector3Int, int, bool> NextStageCondition = (a, b) => true;

        protected HashSet<Vector3Int> chunkIdsInStage = new HashSet<Vector3Int>();

        public PipelineStage(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public PipelineStage(string name, int order, Func<Vector3Int, int, bool> nextStageCondition) : this(name, order)
        {
            NextStageCondition = nextStageCondition;
        }

        public virtual void Update(out List<Vector3Int> movingOn, out List<Vector3Int> terminating)
        {
            var movingOnTmp = new List<Vector3Int>();
            var terminatingTmp = new List<Vector3Int>();

            chunkIdsInStage.RemoveWhere((item)=> {
                if (NextStageCondition(item, Order))
                {
                    movingOnTmp.Add(item);
                }
                else
                {
                    terminatingTmp.Add(item);
                }
                //Always remove
                return true;

            });

            movingOn = movingOnTmp;
            terminating = terminatingTmp;

            //All chunks in the stage have either moved on or terminated here
            Assert.AreEqual(0, chunkIdsInStage.Count);
        }

        public bool Contains(Vector3Int chunkID)
        {
            return chunkIdsInStage.Contains(chunkID);
        }

        public virtual void AddAll(List<Vector3Int> incoming)
        {
            chunkIdsInStage.UnionWith(incoming);
        }

        public virtual void Add(Vector3Int incoming) 
        {
            chunkIdsInStage.Add(incoming);
        }
    }
}