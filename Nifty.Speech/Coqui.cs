using Nifty.Speech.Recognition.Coqui.Enums;
using Nifty.Speech.Recognition.Coqui.Extensions;
using System.Runtime.InteropServices;
using System.Text;

namespace Nifty.Speech.Recognition.Coqui
{
    // https://github.com/coqui-ai/STT
    // https://github.com/coqui-ai/STT/tree/main/native_client/dotnet

    namespace Models
    {
        public class CandidateTranscript
        {
            public double Confidence { get; set; }
            public TokenMetadata[] Tokens { get; set; }
        }

        public class Metadata
        {
            public CandidateTranscript[] Transcripts { get; set; }
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

        public class TokenMetadata
        {
            public string Text;
            public int Timestep;
            public float StartTime;
        }
    }

    namespace Structs
    {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct CandidateTranscript
        {
            internal unsafe IntPtr tokens;
            internal unsafe int num_tokens;
            internal unsafe double confidence;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct Metadata
        {
            internal unsafe IntPtr transcripts;
            internal unsafe int num_transcripts;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct TokenMetadata
        {
            internal unsafe IntPtr text;
            internal unsafe int timestep;
            internal unsafe float start_time;
        }
    }

    namespace Enums
    {
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

    namespace Extensions
    {
        internal static class NativeExtensions
        {
            internal static string PtrToString(this IntPtr intPtr, bool releasePtr = true)
            {
                int len = 0;
                while (Marshal.ReadByte(intPtr, len) != 0) ++len;
                byte[] buffer = new byte[len];
                Marshal.Copy(intPtr, buffer, 0, buffer.Length);
                if (releasePtr)
                    NativeImp.STT_FreeString(intPtr);
                string result = Encoding.UTF8.GetString(buffer);
                return result;
            }

            private static Models.TokenMetadata PtrToTokenMetadata(this IntPtr intPtr)
            {
                var token = Marshal.PtrToStructure<Structs.TokenMetadata>(intPtr);
                var managedToken = new Models.TokenMetadata
                {
                    Timestep = token.timestep,
                    StartTime = token.start_time,
                    Text = token.text.PtrToString(releasePtr: false)
                };
                return managedToken;
            }

            private static Models.CandidateTranscript PtrToCandidateTranscript(this IntPtr intPtr)
            {
                var managedTranscript = new Models.CandidateTranscript();
                var transcript = Marshal.PtrToStructure<Structs.CandidateTranscript>(intPtr);

                managedTranscript.Tokens = new Models.TokenMetadata[transcript.num_tokens];
                managedTranscript.Confidence = transcript.confidence;

                var sizeOfTokenMetadata = Marshal.SizeOf<Structs.TokenMetadata>();
                for (int i = 0; i < transcript.num_tokens; i++)
                {
                    managedTranscript.Tokens[i] = transcript.tokens.PtrToTokenMetadata();
                    transcript.tokens += sizeOfTokenMetadata;
                }

                return managedTranscript;
            }

            internal static Models.Metadata PtrToMetadata(this IntPtr intPtr)
            {
                var managedMetadata = new Models.Metadata();
                var metadata = Marshal.PtrToStructure<Structs.Metadata>(intPtr);

                managedMetadata.Transcripts = new Models.CandidateTranscript[metadata.num_transcripts];

                var sizeOfCandidateTranscript = Marshal.SizeOf<Structs.CandidateTranscript>();
                for (int i = 0; i < metadata.num_transcripts; i++)
                {
                    managedMetadata.Transcripts[i] = metadata.transcripts.PtrToCandidateTranscript();
                    metadata.transcripts += sizeOfCandidateTranscript;
                }

                NativeImp.STT_FreeMetadata(intPtr);
                return managedMetadata;
            }
        }
    }

    public interface ISpeechToText : IDisposable
    {
        public string Version();
        public int GetModelSampleRate();
        public uint GetModelBeamWidth();
        public void SetModelBeamWidth(uint beamWidth);

        public void EnableExternalScorer(string path);
        public void SetScorerAlphaBeta(float alpha, float beta);
        public void DisableExternalScorer();

        public void AddHotWord(string word, float boost);
        public void EraseHotWord(string word);
        public void ClearHotWords();

        public string? SpeechToText(short[] buffer, uint bufferSize);
        public Models.Metadata? SpeechToTextWithMetadata(short[] buffer, uint bufferSize, uint numResults);

        public Models.STTStream CreateStream();
        public void FreeStream(Models.STTStream stream);

        public void FeedAudioContent(Models.STTStream stream, short[] aBuffer, uint aBufferSize);

        public string? IntermediateDecode(Models.STTStream stream);
        public Models.Metadata? IntermediateDecodeWithMetadata(Models.STTStream stream, uint aNumResults);

        public string? FinishStream(Models.STTStream stream);
        public Models.Metadata? FinishStreamWithMetadata(Models.STTStream stream, uint aNumResults);
    }

    public static class SpeechToText
    {
        public static ISpeechToText CreateLocalInstallation(string modelPath)
        {
            return new LocalInstallation(modelPath);
        }
        public static ISpeechToText CreateClient(string uri /* ... */)
        {
            throw new NotImplementedException();
        }
    }

    internal static class NativeImp
    {
        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr STT_Version();

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern ErrorCodes STT_CreateModel(string aModelPath, ref IntPtr** pint);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern IntPtr STT_ErrorCodeToErrorMessage(int aErrorCode);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern uint STT_GetModelBeamWidth(IntPtr** aCtx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern ErrorCodes STT_SetModelBeamWidth(IntPtr** aCtx, uint aBeamWidth);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern ErrorCodes STT_CreateModel(string aModelPath, uint aBeamWidth, ref IntPtr** pint);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal unsafe static extern int STT_GetModelSampleRate(IntPtr** aCtx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_EnableExternalScorer(IntPtr** aCtx, string aScorerPath);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_AddHotWord(IntPtr** aCtx, string aWord, float aBoost);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_EraseHotWord(IntPtr** aCtx, string aWord);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_ClearHotWords(IntPtr** aCtx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_DisableExternalScorer(IntPtr** aCtx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_SetScorerAlphaBeta(IntPtr** aCtx, float aAlpha, float aBeta);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
        internal static unsafe extern IntPtr STT_SpeechToText(IntPtr** aCtx, short[] aBuffer, uint aBufferSize);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static unsafe extern IntPtr STT_SpeechToTextWithMetadata(IntPtr** aCtx, short[] aBuffer, uint aBufferSize, uint aNumResults);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern void STT_FreeModel(IntPtr** aCtx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern ErrorCodes STT_CreateStream(IntPtr** aCtx, ref IntPtr** retval);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern void STT_FreeStream(IntPtr** aSctx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern void STT_FreeMetadata(IntPtr metadata);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern void STT_FreeString(IntPtr str);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
        internal static unsafe extern void STT_FeedAudioContent(IntPtr** aSctx, short[] aBuffer, uint aBufferSize);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern IntPtr STT_IntermediateDecode(IntPtr** aSctx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern IntPtr STT_IntermediateDecodeWithMetadata(IntPtr** aSctx, uint aNumResults);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
        internal static unsafe extern IntPtr STT_FinishStream(IntPtr** aSctx);

        [DllImport("libstt.so", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern IntPtr STT_FinishStreamWithMetadata(IntPtr** aSctx, uint aNumResults);
    }

    internal class LocalInstallation : ISpeechToText
    {
        private unsafe IntPtr** _modelStatePP;

        public LocalInstallation(string aModelPath)
        {
            CreateModel(aModelPath);
        }

        private unsafe void CreateModel(string aModelPath)
        {
            string? exceptionMessage = null;
            if (string.IsNullOrWhiteSpace(aModelPath))
            {
                exceptionMessage = "Model path cannot be empty.";
            }
            if (!File.Exists(aModelPath))
            {
                exceptionMessage = $"Cannot find the model file: {aModelPath}";
            }

            if (exceptionMessage != null)
            {
                throw new FileNotFoundException(exceptionMessage);
            }
            var resultCode = NativeImp.STT_CreateModel(aModelPath, ref _modelStatePP);
            EvaluateResultCode(resultCode);
        }

        public unsafe uint GetModelBeamWidth()
        {
            return NativeImp.STT_GetModelBeamWidth(_modelStatePP);
        }

        public unsafe void SetModelBeamWidth(uint aBeamWidth)
        {
            var resultCode = NativeImp.STT_SetModelBeamWidth(_modelStatePP, aBeamWidth);
            EvaluateResultCode(resultCode);
        }

        public unsafe void AddHotWord(string aWord, float aBoost)
        {
            var resultCode = NativeImp.STT_AddHotWord(_modelStatePP, aWord, aBoost);
            EvaluateResultCode(resultCode);
        }

        public unsafe void EraseHotWord(string aWord)
        {
            var resultCode = NativeImp.STT_EraseHotWord(_modelStatePP, aWord);
            EvaluateResultCode(resultCode);
        }

        public unsafe void ClearHotWords()
        {
            var resultCode = NativeImp.STT_ClearHotWords(_modelStatePP);
            EvaluateResultCode(resultCode);
        }

        public unsafe int GetModelSampleRate()
        {
            return NativeImp.STT_GetModelSampleRate(_modelStatePP);
        }

        private void EvaluateResultCode(ErrorCodes resultCode)
        {
            if (resultCode != ErrorCodes.STT_ERR_OK)
            {
                throw new ArgumentException(NativeImp.STT_ErrorCodeToErrorMessage((int)resultCode).PtrToString());
            }
        }

        public unsafe void Dispose()
        {
            NativeImp.STT_FreeModel(_modelStatePP);
        }

        public unsafe void EnableExternalScorer(string aScorerPath)
        {
            if (string.IsNullOrWhiteSpace(aScorerPath))
            {
                throw new FileNotFoundException("Path to the scorer file cannot be empty.");
            }
            if (!File.Exists(aScorerPath))
            {
                throw new FileNotFoundException($"Cannot find the scorer file: {aScorerPath}");
            }

            var resultCode = NativeImp.STT_EnableExternalScorer(_modelStatePP, aScorerPath);
            EvaluateResultCode(resultCode);
        }

        public unsafe void DisableExternalScorer()
        {
            var resultCode = NativeImp.STT_DisableExternalScorer(_modelStatePP);
            EvaluateResultCode(resultCode);
        }

        public unsafe void SetScorerAlphaBeta(float aAlpha, float aBeta)
        {
            var resultCode = NativeImp.STT_SetScorerAlphaBeta(_modelStatePP,
                            aAlpha,
                            aBeta);
            EvaluateResultCode(resultCode);
        }

        public unsafe void FeedAudioContent(Models.STTStream stream, short[] aBuffer, uint aBufferSize)
        {
            NativeImp.STT_FeedAudioContent(stream.GetNativePointer(), aBuffer, aBufferSize);
        }

        public unsafe string FinishStream(Models.STTStream stream)
        {
            return NativeImp.STT_FinishStream(stream.GetNativePointer()).PtrToString();
        }

        public unsafe Models.Metadata? FinishStreamWithMetadata(Models.STTStream stream, uint aNumResults)
        {
            return NativeImp.STT_FinishStreamWithMetadata(stream.GetNativePointer(), aNumResults).PtrToMetadata();
        }

        public unsafe string? IntermediateDecode(Models.STTStream stream)
        {
            return NativeImp.STT_IntermediateDecode(stream.GetNativePointer()).PtrToString();
        }

        public unsafe Models.Metadata? IntermediateDecodeWithMetadata(Models.STTStream stream, uint aNumResults)
        {
            return NativeImp.STT_IntermediateDecodeWithMetadata(stream.GetNativePointer(), aNumResults).PtrToMetadata();
        }

        public unsafe string Version()
        {
            return NativeImp.STT_Version().PtrToString();
        }

        public unsafe Models.STTStream CreateStream()
        {
            IntPtr** streamingStatePointer = null;
            var resultCode = NativeImp.STT_CreateStream(_modelStatePP, ref streamingStatePointer);
            EvaluateResultCode(resultCode);
            return new Models.STTStream(streamingStatePointer);
        }

        public unsafe void FreeStream(Models.STTStream stream)
        {
            NativeImp.STT_FreeStream(stream.GetNativePointer());
            stream.Dispose();
        }

        public unsafe string? SpeechToText(short[] aBuffer, uint aBufferSize)
        {
            return NativeImp.STT_SpeechToText(_modelStatePP, aBuffer, aBufferSize).PtrToString();
        }

        public unsafe Models.Metadata? SpeechToTextWithMetadata(short[] aBuffer, uint aBufferSize, uint aNumResults)
        {
            return NativeImp.STT_SpeechToTextWithMetadata(_modelStatePP, aBuffer, aBufferSize, aNumResults).PtrToMetadata();
        }
    }
}

namespace Nifty.Speech.Synthesis.Coqui
{
    // https://github.com/coqui-ai/TTS
}