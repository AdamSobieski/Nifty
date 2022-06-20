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

    internal enum ErrorCodes
    {
        STT_ERR_OK = 0x0000,

        STT_ERR_NO_MODEL = 0x1000,

        STT_ERR_INVALID_ALPHABET = 0x2000,
        STT_ERR_INVALID_SHAPE = 0x2001,
        STT_ERR_INVALID_SCORER = 0x2002,
        STT_ERR_MODEL_INCOMPATIBLE = 0x2003,
        STT_ERR_SCORER_NOT_ENABLED = 0x2004,

        STT_ERR_FAIL_INIT_MMAP = 0x3000,
        STT_ERR_FAIL_INIT_SESS = 0x3001,
        STT_ERR_FAIL_INTERPRETER = 0x3002,
        STT_ERR_FAIL_RUN_SESS = 0x3003,
        STT_ERR_FAIL_CREATE_STREAM = 0x3004,
        STT_ERR_FAIL_READ_PROTOBUF = 0x3005,
        STT_ERR_FAIL_CREATE_SESS = 0x3006,
        STT_ERR_FAIL_INSERT_HOTWORD = 0x3008,
        STT_ERR_FAIL_CLEAR_HOTWORD = 0x3009,
        STT_ERR_FAIL_ERASE_HOTWORD = 0x3010
    }
}

namespace Nifty.Speech.Synthesis.Coqui
{
    // https://github.com/coqui-ai/TTS
}