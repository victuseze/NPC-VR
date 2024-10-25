using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LlamaSpeechHandler : MonoBehaviour
{
    private const string LlamaAPIUrl = "https://api-inference.huggingface.co/models/meta-llama/Llama-3.2-1B";
    private const string API_KEY = "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";  // Your actual Llama API key

    [SerializeField] private AudioSource audioSource;  // Reference to AudioSource component

    void Start()
    {
        // Initialize the AudioSource component
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // Method to start the speech recognition and audio playback process
    public void StartSpeechRecognition(string inputText)
    {
        Debug.Log("Starting Speech Recognition with input: " + inputText);
        StartCoroutine(GetCleanTextAndAudioFromLlama(inputText));
    }

    // Coroutine to send request to Llama API and retrieve clean text and audio in base64 format
    private IEnumerator GetCleanTextAndAudioFromLlama(string inputText)
    {
        // Prepare JSON data for the Llama API request
        string jsonData = JsonUtility.ToJson(new { text = inputText });
        Debug.Log("Sending request with JSON data: " + jsonData);

        using (UnityWebRequest webRequest = new UnityWebRequest(LlamaAPIUrl, "POST"))
        {
            webRequest.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            webRequest.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // Send the request and wait for a response
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Received response from Llama API.");

                // Log the raw response from the API
                string responseText = webRequest.downloadHandler.text;
                Debug.Log("Llama API Response: " + responseText);

                // Parse and extract base64 audio data
                string base64Audio = ParseBase64Audio(responseText);
                
                if (!string.IsNullOrEmpty(base64Audio))
                {
                    Debug.Log("Base64 Audio Length: " + base64Audio.Length);
                    // Play audio from base64
                    PlayAudioFromBase64(base64Audio);
                }
                else
                {
                    Debug.LogError("No audio data returned or audio data is empty.");
                }
            }
            else
            {
                // Log error if the request fails
                Debug.LogError($"Error during Llama request: {webRequest.error} | Status: {webRequest.responseCode}");
            }
        }
    }

    // Parse the base64 audio string from Llama's API response
    private string ParseBase64Audio(string responseText)
    {
        // Attempt to deserialize the JSON response to extract the audio base64 string
        try
        {
            var response = JsonUtility.FromJson<LlamaResponse>(responseText);
            if (response != null && !string.IsNullOrEmpty(response.audio_base64))
            {
                Debug.Log("Successfully parsed base64 audio from response.");
                return response.audio_base64;  // Return the audio_base64 string if valid
            }
            else
            {
                Debug.LogError("No audio_base64 field found in the response.");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error parsing Llama response: " + e.Message);
            return null;
        }
    }

    // Method to convert base64 audio and play it
    private void PlayAudioFromBase64(string base64Audio)
    {
        Debug.Log("Attempting to play audio from base64.");

        // Decode the base64 audio
        byte[] audioData = WaveUtility.FromBase64(base64Audio);

        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("Failed to decode the base64 audio data. Audio data is null or empty.");
            return;
        }

        Debug.Log("Audio data successfully decoded. Length: " + audioData.Length);

        // Convert the byte array into an AudioClip
        AudioClip audioClip = WaveUtility.FromWaveData(audioData);

        // Additional checks for valid audio clip
        if (audioClip == null || audioClip.samples == 0)
        {
            Debug.LogError("TTS: Audio data is empty or invalid.");
        }
        else
        {
            Debug.Log("Audio clip successfully generated. Playing audio...");
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }

    // Define a class to map the JSON response from the Llama API
    [System.Serializable]
    private class LlamaResponse
    {
        public string audio_base64;  // The base64 encoded audio data
    }
}
