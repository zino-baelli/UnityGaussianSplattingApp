namespace SplattingPipeline
{
    public class PipelineSettings
    {
        public PipelineSettings(){}

        public PipelineSettings(int maxResolution, int numIterations)
        {
            MaxResolution = maxResolution;
            NumIterations = numIterations;
        }
        public int MaxResolution { get; set; } = 512;
        public int NumIterations { get; set; } = 1000;
        public int ExtractionFramerate { get; set; } = 2;
    }
}