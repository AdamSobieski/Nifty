namespace Nifty.Speech.Recognition.Coqui
{
    // https://github.com/coqui-ai/STT
    // https://github.com/coqui-ai/STT/tree/main/native_client/dotnet

    public interface ISpeechToText : IDisposable
    {
        string Version { get; }
        uint ModelSampleRate { get; }
        uint ModelBeamWidth { get; set; }

        void EnableExternalScorer(string path);
        void SetScorerAlphaBeta(float alpha, float beta);
        void DisableExternalScorer();

        void AddHotWord(string word, float boost);
        void EraseHotWord(string word);
        void ClearHotWords();

        string? SpeechToText(short[] buffer, uint bufferSize);
        Metadata? SpeechToTextWithMetadata(short[] buffer, uint bufferSize, uint numResults);

        STTStream CreateStream();
        void FreeStream(STTStream stream);

        void FeedAudioContent(STTStream stream, short[] aBuffer, uint aBufferSize);

        string? IntermediateDecode(STTStream stream);
        Metadata? IntermediateDecodeWithMetadata(STTStream stream, uint aNumResults);

        string? FinishStream(STTStream stream);
        Metadata? FinishStreamWithMetadata(STTStream stream, uint aNumResults);
    }

    public class Metadata
    {
        public CandidateTranscript[] Transcripts { get; set; }
    }

    public class CandidateTranscript
    {
        public double Confidence { get; set; }
        public TokenMetadata[] Tokens { get; set; }
    }

    public class TokenMetadata
    {
        public string Text;
        public int Timestep;
        public float StartTime;
    }

    public class STTStream : IDisposable
    {
        private unsafe IntPtr** _streamingStatePp;

        public unsafe STTStream(IntPtr** streamingStatePP)
        {
            _streamingStatePp = streamingStatePP;
        }

        internal unsafe IntPtr** GetNativePointer()
        {
            if (_streamingStatePp == null)
                throw new InvalidOperationException("Cannot use a disposed or uninitialized stream.");
            return _streamingStatePp;
        }

        public unsafe void Dispose() => _streamingStatePp = null;
    }
}

namespace Nifty.Speech.Synthesis.Coqui
{
    // https://github.com/coqui-ai/TTS
}