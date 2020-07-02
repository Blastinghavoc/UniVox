namespace UniVox.Framework.ChunkPipeline.WaitForNeighbours
{
    public interface INeighbourStatus
    {
        bool AllValid { get; }

        void AddNeighbour(int direction);

        void RemoveNeighbour(int direction);
    }

}