namespace UniVox.Framework.ChunkPipeline
{
    [System.Serializable]
    public class ChunkStageData
    {
        public int maxStage = 0;
        public int minStage = 0;
        public int targetStage;

        public bool WorkInProgress { get => minStage < targetStage || minStage < maxStage; }
    }    
}