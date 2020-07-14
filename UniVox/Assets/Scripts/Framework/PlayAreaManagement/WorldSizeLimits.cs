using UnityEngine;

namespace UniVox.Framework.PlayAreaManagement
{
    [System.Serializable]
    public class WorldSizeLimits 
    {
        //Should the world height be limited (like minecraft)
        [SerializeField] private bool limitWorldHeight = false;
        //Vertical chunk limit of 8 -> max chunkid Y coordinate is 7, min -8
        [SerializeField] private int verticalChunkLimit = int.MaxValue;

        public WorldSizeLimits(bool limitWorldHeight, int verticalChunkLimit)
        {
            this.limitWorldHeight = limitWorldHeight;
            this.verticalChunkLimit = verticalChunkLimit;
        }

        public bool IsWorldHeightLimited { get => limitWorldHeight; }
        public int MaxChunkY { get; private set; }
        public int MinChunkY { get; private set; }
        public int HeightLimit { get => verticalChunkLimit; }
        public void Initalise() 
        {
            //Set up world height limits
            MaxChunkY = limitWorldHeight ? verticalChunkLimit - 1 : int.MaxValue;
            MinChunkY = limitWorldHeight ? -verticalChunkLimit : int.MinValue;
        }
    }
}