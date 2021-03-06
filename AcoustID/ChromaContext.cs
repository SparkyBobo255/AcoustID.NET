﻿// -----------------------------------------------------------------------
// <copyright file="ChromaContext.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID
{
    using AcoustID.Audio;
    using AcoustID.Chromaprint;
    using AcoustID.Util;
    using System;

    /// <summary>
    /// The main Chromaprint API.
    /// </summary>
    public class ChromaContext : IChromaContext
    {
        /// <summary>
        /// Return the version number of Chromaprint.
        /// </summary>
        public static string GetVersion()
        {
            return "1.3.2";
        }

        /// <summary>
        /// Gets the fingerprint algorithm this context is configured to use.
        /// </summary>
        public int Algorithm { get; }

        /// <summary>
        /// Return the version number of Chromaprint.
        /// </summary>
        public string Version
        {
            get { return GetVersion(); }
        }

        Fingerprinter fingerprinter;

        IFFTService fftService;

        int[] fingerprint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromaContext" /> class.
        /// </summary>
        public ChromaContext()
            : this(ChromaprintAlgorithm.TEST2)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromaContext" /> class.
        /// </summary>
        /// <param name="fftService">The FFT service.</param>
        public ChromaContext(IFFTService fftService)
            : this(ChromaprintAlgorithm.TEST2, fftService)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromaContext" /> class.
        /// </summary>
        /// <param name="algorithm">The algorithm to use, see <see cref="ChromaprintAlgorithm" /> (default = TEST2)</param>
        public ChromaContext(ChromaprintAlgorithm algorithm)
            : this(ChromaprintAlgorithm.TEST2, new LomontFFTService())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromaContext" /> class.
        /// </summary>
        /// <param name="algorithm">The algorithm to use, see <see cref="ChromaprintAlgorithm" /> (default = TEST2)</param>
        /// <param name="fftService">The FFT service.</param>
        public ChromaContext(ChromaprintAlgorithm algorithm, IFFTService fftService)
        {
            this.Algorithm = (int)algorithm;
            this.fftService = fftService;

            var config = FingerprinterConfiguration.CreateConfiguration(algorithm);
            this.fingerprinter = new Fingerprinter(config, fftService);
        }

        #region Audio consumer interface

        /// <summary>
        /// Send audio data to the fingerprint calculator (alias to Feed() method).
        /// </summary>
        /// <param name="data">raw audio data, should point to an array of 16-bit 
        /// signed integers in native byte-order</param>
        /// <param name="size">size of the data buffer (in samples)</param>
        public void Consume(short[] data, int size)
        {
            fingerprinter.Consume(data, size);
        }

        #endregion

        /// <summary>
        /// Set a configuration option for the selected fingerprint algorithm.
        /// </summary>
        /// <param name="name">option name</param>
        /// <param name="value">option value</param>
        /// <returns>False on error, true on success</returns>
        /// <remarks>
        /// NOTE: DO NOT USE THIS FUNCTION IF YOU ARE PLANNING TO USE
        /// THE GENERATED FINGERPRINTS WITH THE ACOUSTID SERVICE.
        /// 
        /// Possible options:
        ///  - silence_threshold: threshold for detecting silence, 0-32767
        /// </remarks>
        public bool SetOption(string name, int value)
        {
            return fingerprinter.SetOption(name, value);
        }

        /// <summary>
        /// Restart the computation of a fingerprint with a new audio stream
        /// </summary>
        /// <param name="sample_rate">sample rate of the audio stream (in Hz)</param>
        /// <param name="num_channels">numbers of channels in the audio stream (1 or 2)</param>
        /// <returns>False on error, true on success</returns>
        public bool Start(int sample_rate, int num_channels)
        {
            return fingerprinter.Start(sample_rate, num_channels);
        }

        /// <summary>
        /// Send audio data to the fingerprint calculator.
        /// </summary>
        /// <param name="data">raw audio data, should point to an array of 16-bit 
        /// signed integers in native byte-order</param>
        /// <param name="size">size of the data buffer (in samples)</param>
        public void Feed(short[] data, int size)
        {
            fingerprinter.Consume(data, size);
        }

        /// <summary>
        /// Process any remaining buffered audio data and calculate the fingerprint.
        /// </summary>
        public void Finish()
        {
            fingerprint = fingerprinter.Finish();
        }

        /// <summary>
        /// Return the calculated fingerprint as a compressed string.
        /// </summary>
        /// <returns>The fingerprint as a compressed string</returns>
        public string GetFingerprint()
        {
            FingerprintCompressor compressor = new FingerprintCompressor();
            return Base64.Encode(compressor.Compress(this.fingerprint, Algorithm));
        }

        /// <summary>
        /// Return the calculated fingerprint as an array of 32-bit integers.
        /// </summary>
        /// <returns>The raw fingerprint (array of 32-bit integers)</returns>
        public int[] GetRawFingerprint()
        {
            int size = this.fingerprint.Length;
            int[] fp = new int[size];

            // TODO: copying necessary?
            Array.Copy(this.fingerprint, fp, size);
            return fp;
        }

        /// <summary>
        /// Compress and optionally base64-encode a raw fingerprint.
        /// </summary>
        /// <param name="fp">Pointer to an array of 32-bit integers representing the raw fingerprint to be encoded.</param>
        /// <param name="algorithm">Chromaprint algorithm version which was used to generate the raw fingerprint.</param>
        /// <param name="base64">Whether to return binary data or base64-encoded ASCII data.</param>
        /// <returns>The encoded fingerprint.</returns>
        public static byte[] EncodeFingerprint(int[] fp, int algorithm, bool base64)
        {
            FingerprintCompressor compressor = new FingerprintCompressor();
            string compressed = compressor.Compress(fp, algorithm);

            if (!base64)
            {
                return Base64.ByteEncoding.GetBytes(compressed);
            }

            return Base64.ByteEncoding.GetBytes(Base64.Encode(compressed));
        }

        /// <summary>
        /// Uncompress and optionally base64-decode an encoded fingerprint.
        /// </summary>
        /// <param name="encoded_fp">Pointer to an encoded fingerprint.</param>
        /// <param name="base64">Whether the encoded_fp parameter contains binary data or base64-encoded ASCII data.</param>
        /// <param name="algorithm">Chromaprint algorithm version which was used to generate the raw fingerprint.</param>
        /// <returns>The decoded raw fingerprint (array of 32-bit integers).</returns>
        public static int[] DecodeFingerprint(byte[] encoded_fp, bool base64, out int algorithm)
        {
            string encoded = Base64.ByteEncoding.GetString(encoded_fp);
            string compressed = base64 ? Base64.Decode(encoded) : encoded;

            FingerprintDecompressor decompressor = new FingerprintDecompressor();
            int[] uncompressed = decompressor.Decompress(compressed, out algorithm);

            int size = uncompressed.Length;
            int[] fp = new int[size];
            // TODO: copying necessary?
            Array.Copy(uncompressed, fp, size);

            return fp;
        }

        /// <summary>
        /// Return 32-bit hash of the calculated fingerprint.
        /// </summary>
        /// <returns>The hash.</returns>
        /// <remarks>
        /// If two fingerprints are similar, their hashes generated by this function
        /// will also be similar. If they are significantly different, their hashes
        /// will most likely be significantly different as well, but you can't rely
        /// on that.
        ///
        /// You compare two hashes by counting the bits in which they differ. Normally
        /// that would be something like POPCNT(hash1 XOR hash2), which returns a
        /// number between 0 and 32. Anthing above 15 means the hashes are
        /// completely different.
        /// </remarks>
        public int GetFingerprintHash()
        {
            return SimHash.Compute(fingerprint);
        }
    }
}
