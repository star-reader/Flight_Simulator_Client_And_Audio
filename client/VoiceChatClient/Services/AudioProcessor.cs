using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace VoiceChatClient.Services {
    public class AudioProcessor {
        private readonly ConcurrentDictionary<string, AudioStream> activeStreams;
        private readonly float sampleRate = 44100;
        private readonly int fftLength = 2048;
        private readonly float[] window;
        
        public AudioProcessor() {
            activeStreams = new ConcurrentDictionary<string, AudioStream>();
            window = CreateHannWindow(fftLength);
        }

        // 添加新的音频流
        public void AddStream(string username, byte[] audioData) {
            var stream = new AudioStream {
                Username = username,
                Buffer = new float[audioData.Length / 2],
                LastUpdate = DateTime.Now
            };

            // 转换字节数据为float数组
            for (int i = 0; i < audioData.Length; i += 2) {
                stream.Buffer[i / 2] = BitConverter.ToInt16(audioData, i) / 32768f;
            }

            activeStreams.AddOrUpdate(username, stream, (key, old) => stream);
        }

        // 处理所有活跃音频流并生成最终输出
        public byte[] ProcessAudio() {
            // 清理过期流
            var now = DateTime.Now;
            foreach (var stream in activeStreams) {
                if ((now - stream.Value.LastUpdate).TotalMilliseconds > 200) {
                    activeStreams.TryRemove(stream.Key, out _);
                }
            }

            if (activeStreams.Count == 0) return new byte[0];
            if (activeStreams.Count == 1) {
                // 单个流不需要处理冲突
                return ConvertToBytes(activeStreams.First().Value.Buffer);
            }

            // 多个流需要处理冲突
            var mixedAudio = new float[fftLength];
            var fftBuffer = new Complex[fftLength];

            // 对每个流进行FFT
            foreach (var stream in activeStreams.Values) {
                // 应用窗函数
                for (int i = 0; i < fftLength; i++) {
                    fftBuffer[i] = new Complex(
                        stream.Buffer[i % stream.Buffer.Length] * window[i], 0);
                }
                // FFT
                FFT(fftBuffer, false);
                // 添加干扰
                AddInterference(fftBuffer);
                // IFFT
                FFT(fftBuffer, true);
                // 累加到混合缓冲区
                for (int i = 0; i < fftLength; i++) {
                    mixedAudio[i] += (float)(fftBuffer[i].Real / fftLength);
                }
            }

            // 添加白噪声
            AddWhiteNoise(mixedAudio, activeStreams.Count);å
            // 应用压限器
            ApplyLimiter(mixedAudio);

            return ConvertToBytes(mixedAudio);
        }

        private void AddInterference(Complex[] buffer) {
            // 模拟AM调制干扰
            var carrierFreq = 1000; // 1kHz载波
            var modulationIndex = 0.3f;
            
            for (int i = 0; i < buffer.Length; i++) {
                var freq = i * sampleRate / buffer.Length;
                if (Math.Abs(freq - carrierFreq) < 100) {
                    // 在载波频率附近添加边带
                    var sidebandGain = Math.Exp(-Math.Pow(freq - carrierFreq, 2) / 2000);
                    buffer[i] *= (1 + modulationIndex * sidebandGain);
                }
            }
        }

        private void AddWhiteNoise(float[] buffer, int streamCount) {
            var random = new Random();
            var noiseLevel = 0.02f * (streamCount - 1); // 噪声随说话人数增加

            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] += (float)((random.NextDouble() * 2 - 1) * noiseLevel);
            }
        }

        private void ApplyLimiter(float[] buffer) {
            // 软削波限制器
            float threshold = 0.8f;
            float ratio = 4.0f;

            for (int i = 0; i < buffer.Length; i++) {
                float abs = Math.Abs(buffer[i]);
                if (abs > threshold) {
                    float excess = abs - threshold;
                    float reduction = excess / ratio;
                    buffer[i] *= (threshold + reduction) / abs;
                }
            }
        }

        private float[] CreateHannWindow(int length) {
            var window = new float[length];
            for (int i = 0; i < length; i++) {
                window[i] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * i / (length - 1)));
            }
            return window;
        }

        private void FFT(Complex[] buffer, bool inverse) {
            int n = buffer.Length;
            if ((n & (n - 1)) != 0) throw new ArgumentException("Buffer length must be power of 2");

            // 位反转
            int j = 0;
            for (int i = 0; i < n - 1; i++) {
                if (i < j) {
                    var temp = buffer[i];
                    buffer[i] = buffer[j];
                    buffer[j] = temp;
                }
                int k = n >> 1;
                while (k <= j) {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // 蝶形运算
            for (int step = 1; step < n; step <<= 1) {
                double theta = (inverse ? 2 : -2) * Math.PI / (2 * step);
                Complex wn = new Complex(Math.Cos(theta), Math.Sin(theta));

                for (int group = 0; group < n; group += 2 * step) {
                    Complex w = Complex.One;
                    for (int pair = group; pair < group + step; pair++) {
                        Complex temp = w * buffer[pair + step];
                        buffer[pair + step] = buffer[pair] - temp;
                        buffer[pair] += temp;
                        w *= wn;
                    }
                }
            }

            if (inverse) {
                for (int i = 0; i < n; i++)
                    buffer[i] /= n;
            }
        }

        private byte[] ConvertToBytes(float[] buffer) {
            var bytes = new byte[buffer.Length * 2];
            for (int i = 0; i < buffer.Length; i++) {
                var sample = (short)(buffer[i] * 32767f);
                bytes[i * 2] = (byte)(sample & 0xFF);
                bytes[i * 2 + 1] = (byte)(sample >> 8);
            }
            return bytes;
        }

        private class AudioStream {
            public string Username { get; set; }
            public float[] Buffer { get; set; }
            public DateTime LastUpdate { get; set; }
        }
    }
} 