using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class SpeechRecognitionTest : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private AudioSource speakerAudioSource; // The speaker's AudioSource to play the audio

    private AudioClip clip;
    private bool recording;

    // Max duration for each recording (in seconds)
    private const int MaxRecordingDuration = 5;

    private void Start()
    {
        startButton.onClick.AddListener(StartRecording);
        stopButton.onClick.AddListener(StopRecording);
        stopButton.interactable = false;

        // Ensure speakerAudioSource is set
        if (speakerAudioSource == null)
        {
            Debug.LogError("No AudioSource assigned to the speaker.");
        }
    }

    private void Update()
    {
        if (recording && Microphone.GetPosition(null) >= clip.samples)
        {
            StopRecording();
        }
    }

    private void StartRecording()
    {
        text.color = Color.white;
        text.text = "Recording...";
        startButton.interactable = false;
        stopButton.interactable = true;

        // Start recording with a max duration of MaxRecordingDuration
        clip = Microphone.Start(null, false, MaxRecordingDuration, 44100);
        recording = true;
        Debug.Log("Started recording audio...");
    }

    private void StopRecording()
    {
        var position = Microphone.GetPosition(null);
        Microphone.End(null);
        var samples = new float[position * clip.channels];
        clip.GetData(samples, 0);

        // Convert the audio into WAV format
        byte[] audioData = EncodeAsWAV(samples, clip.frequency, clip.channels);

        // Use Speech-to-Text model to convert audio into text
        text.color = Color.yellow;
        text.text = "Sending to Speech-to-Text Model...";
        SendToSpeechToTextAPI(audioData);
    }

    private async void SendToSpeechToTextAPI(byte[] audioData)
    {
        string url = "https://api-inference.huggingface.co/models/openai/whisper-large";
        string apiKey = "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"; // Replace with your Hugging Face API Key

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var content = new ByteArrayContent(audioData);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    string transcribedText = ParseTranscription(responseBody); // Parse the transcription

                    text.color = Color.green;
                    text.text = "Transcribed: " + transcribedText;
                    Debug.Log($"Transcription: {transcribedText}");

                    // Now send the transcribed text to Llama
                    await SendToLlamaAPI(transcribedText);
                }
                else
                {
                    text.color = Color.red;
                    text.text = $"Error: {response.StatusCode}";
                    Debug.LogError($"Bad response: {response.StatusCode}. Reason: {response.ReasonPhrase}");
                }
            }
            catch (Exception e)
            {
                text.color = Color.red;
                text.text = "Error: " + e.Message;
                Debug.LogError($"Request failed: {e.Message}");
            }
            finally
            {
                startButton.interactable = true;
                stopButton.interactable = false;
            }
        }
    }

    private async Task SendToLlamaAPI(string textPrompt)
    {
        string url = "https://api-inference.huggingface.co/models/meta-llama/Llama-3.2-1B";
        string apiKey = "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"; // Replace with your Hugging Face API Key

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var content = new StringContent($"{{\"inputs\": \"{textPrompt}\"}}");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Parse the response as an array
                    string llamaResponse = ParseLlamaResponse(responseBody);
                    text.color = Color.green;
                    text.text = "Llama Response: " + llamaResponse;
                    Debug.Log($"Llama Response: {llamaResponse}");

                    // Play the response audio on the specified speaker
                    PlayLlamaResponseAudio(llamaResponse);
                }
                else
                {
                    text.color = Color.red;
                    text.text = $"Error: {response.StatusCode}";
                    Debug.LogError($"Bad response: {response.StatusCode}. Reason: {response.ReasonPhrase}");
                }
            }
            catch (Exception e)
            {
                text.color = Color.red;
                text.text = "Error: " + e.Message;
                Debug.LogError($"Request failed: {e.Message}");
            }
        }
    }

    private string ParseTranscription(string responseBody)
    {
        // Assuming the response is a JSON object and contains a 'text' field with the transcribed text
        var jsonResponse = JsonUtility.FromJson<SpeechToTextResponse>(responseBody);
        return jsonResponse.text;
    }

    private string ParseLlamaResponse(string responseBody)
    {
        try
        {
            // Since we're getting an array, we need to handle it accordingly
            LlamaResponse[] responses = JsonHelper.FromJsonArray<LlamaResponse>(responseBody);

            // Return the first response, or indicate if there is no response
            return responses.Length > 0 ? responses[0].generated_text : "No response from Llama.";
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to parse Llama response: " + e.Message);
            return "Error parsing response.";
        }
    }

    // Play the generated text as audio through the assigned speaker
    // Play the generated text as audio through the assigned speaker
    // Play the generated text as audio through the assigned speaker
    private void PlayLlamaResponseAudio(string response)
    {
        if (speakerAudioSource != null)
        {
            // Generate the speech from the text (e.g., using text-to-speech API)
            Debug.Log("Playing audio on specified speaker: " + response);

            // Assume the response text is an audio stream or base64 encoded audio
            byte[] audioData = ConvertTextToAudio(response);

            if (audioData != null && audioData.Length > 0)
            {
                // Create an AudioClip and load the byte data (assuming mono channel, 44100 sample rate)
                int sampleCount = audioData.Length / 2; // Assuming each sample is 2 bytes (16-bit audio)
                AudioClip audioClip = AudioClip.Create("LlamaAudio", sampleCount, 1, 44100, false);

                // Convert byte array to float array (PCM data)
                float[] samples = new float[sampleCount];
                for (int i = 0; i < audioData.Length; i += 2)
                {
                    // Convert two bytes to a single float sample
                    short sample = BitConverter.ToInt16(audioData, i);
                    samples[i / 2] = sample / 32768.0f; // Convert to normalized float (-1 to 1)
                }

                // Set the audio data to the AudioClip
                audioClip.SetData(samples, 0);

                // Assign the clip to the AudioSource and play it
                speakerAudioSource.clip = audioClip;
                speakerAudioSource.Play();
            }
            else
            {
                Debug.LogError("Audio data is empty or invalid.");
            }
        }
        else
        {
            Debug.LogError("No AudioSource assigned for speaker.");
        }
    }



    // Method to convert text to audio (assuming this will call a text-to-speech service)
    private byte[] ConvertTextToAudio(string text)
    {
        // For simplicity, this function should use a text-to-speech API to generate audio data from the text
        // For example, you might use Unity's SpeechSynthesis API or an external service
        Debug.Log("Converting text to audio: " + text);

        // Placeholder for actual audio data conversion logic
        return new byte[] {}; // Return audio data here (byte array)
    }

    // Speech-to-text response class
    [Serializable]
    public class SpeechToTextResponse
    {
        public string text;
    }

    // Llama response class
    [Serializable]
    public class LlamaResponse
    {
        public string generated_text;
    }

    // JSON Helper to handle arrays
    public static class JsonHelper
    {
        public static T[] FromJsonArray<T>(string json)
        {
            string newJson = "{\"items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.items;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }

    // Method to encode audio to WAV format
    private byte[] EncodeAsWAV(float[] samples, int sampleRate, int channels)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            // Write WAV header (simplified)
            WriteWAVHeader(stream, samples.Length, sampleRate, channels);
            // Write samples
            WriteSamples(stream, samples);

            return stream.ToArray();
        }
    }

    private void WriteWAVHeader(MemoryStream stream, int sampleCount, int sampleRate, int channels)
    {
        // Standard header info (in bytes)
        stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4); // RIFF header
        stream.Write(BitConverter.GetBytes(sampleCount * 2 + 36), 0, 4); // Chunk size
        stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4); // WAVE format
        stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4); // Format section
        stream.Write(BitConverter.GetBytes(16), 0, 4); // Subchunk1Size
        stream.Write(BitConverter.GetBytes((short)1), 0, 2); // AudioFormat (1: PCM)
        stream.Write(BitConverter.GetBytes((short)channels), 0, 2); // NumChannels
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4); // SampleRate
        stream.Write(BitConverter.GetBytes(sampleRate * channels * 2), 0, 4); // ByteRate
        stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2); // BlockAlign
        stream.Write(BitConverter.GetBytes((short)16), 0, 2); // BitsPerSample
        stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4); // Data section
        stream.Write(BitConverter.GetBytes(sampleCount * 2), 0, 4); // DataSize
    }

    private void WriteSamples(MemoryStream stream, float[] samples)
    {
        foreach (var sample in samples)
        {
            short sampleInt = (short)(sample * short.MaxValue);
            stream.Write(BitConverter.GetBytes(sampleInt), 0, 2);
        }
    }
}
