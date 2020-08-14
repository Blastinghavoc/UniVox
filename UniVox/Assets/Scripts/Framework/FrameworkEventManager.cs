using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniVox.Framework
{
    public class FrameworkEventManager
    {
        #region PlayerChunkChanged
        /// <summary>
        /// Event for when the player chunk changes at all
        /// </summary>
        public event EventHandler<PlayerChunkChangedArgs> OnPlayerChunkChanged = delegate { };
        /// <summary>
        /// When the player chunk X or Z coordinates change
        /// </summary>
        public event EventHandler<PlayerChunkChangedArgs> OnPlayerChunkChangedXZ = delegate { };

        public void FirePlayerChunkChanged(PlayerChunkChangedArgs args)
        {
            OnPlayerChunkChanged(this, args);
            if (args.currentPlayerChunk.x != args.prevPlayerChunk.x ||
                args.currentPlayerChunk.z != args.prevPlayerChunk.z)
            {
                OnPlayerChunkChangedXZ(this, args);
            }
        }

        public class PlayerChunkChangedArgs : EventArgs
        {
            public Vector3Int prevPlayerChunk;
            public Vector3Int currentPlayerChunk;
            public Vector3Int maximumChunkRadii;
        }
        #endregion

        #region ChunkActivated
        public event EventHandler<ChunkActivatedArgs> OnChunkActivated = delegate { };

        public void FireChunkActivated(Vector3Int chunkId)
        {
            Profiler.BeginSample("ChunkActivatedEvent");
            OnChunkActivated(this, new ChunkActivatedArgs() { chunkId = chunkId });
            Profiler.EndSample();
        }

        public class ChunkActivatedArgs : EventArgs
        {
            public Vector3Int chunkId;
        }
        #endregion

        #region ChunkDeactivated
        public event EventHandler<ChunkDeactivatedArgs> OnChunkDeactivated = delegate { };

        public void FireChunkDeactivated(Vector3Int chunkID, Vector3Int playerChunkID, Vector3Int maxChunkRadii)
        {
            Profiler.BeginSample("ChunkDeactivatedEvent");
            var displacement = playerChunkID - chunkID;
            var absDisplacement = displacement.ElementWise(Mathf.Abs);
            var args = new ChunkDeactivatedArgs() { chunkID = chunkID, absAmountOutsideRadii = absDisplacement - maxChunkRadii };
            OnChunkDeactivated(this, args);
            Profiler.EndSample();
        }

        public class ChunkDeactivatedArgs : EventArgs
        {
            public Vector3Int chunkID;
            public Vector3Int absAmountOutsideRadii;
        }
        #endregion
    }
}