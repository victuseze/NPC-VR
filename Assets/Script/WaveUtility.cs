using System;
using System.IO;
using UnityEngine;

public static class WaveUtility
{
    public static AudioClip FromWaveData(byte[] wavFileData)
    {
        // WAV file header contains 44 bytes of metadata before the actual audio data starts
        const int headerSize = 44;

        int sampleCount = wavFileData.Length - headerSize;
        float[] samples = new float[sampleCount / 2]; // 16-bit audio (2 bytes per sample)

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(wavFileData, headerSize + i * 2) / 32768.0f;
        }

        AudioClip audioClip = AudioClip.Create("GeneratedClip", samples.Length, 1, 44100, false);
        audioClip.SetData(samples, 0);
        return audioClip;
    }

    public static byte[] FromBase64(string base64String)
    {
        return Convert.FromBase64String(base64String);
    }
}
