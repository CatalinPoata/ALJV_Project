using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class StoryManager : MonoBehaviour
{
    public UIManager uiManager;
    public MusicManager musicManager;

    private string baseUrl = "https://0de2-34-143-183-43.ngrok-free.app";


    private enum StoryPhase { Waiting, AfterIntro, AfterChoice }
    private StoryPhase currentPhase = StoryPhase.Waiting;


    [Serializable]
    public class PromptRequest { public string prompt; }
    [Serializable]
    public class SummaryResponse { public string image_prompt; }
    [Serializable]
    public class ImageResponse { public string image_base64; }

    [Serializable]
    public class MusicResponse { public string response; }

    private string history = "";
    private string characterJson = "{\n    \"health\": 10,\n    \"weapon\": \"Sword\",\n    \"armor\": \"Armor\",\n    \"gold\": 10,\n    \"items\": [\"Rope\"]\n}";
    private string[] lastChoices;

    public static string CleanJsonString(string rawJson) {
        if (string.IsNullOrEmpty(rawJson))
            return string.Empty;

        rawJson = Regex.Unescape(rawJson);

        string cleaned = rawJson
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");

        return cleaned;
    }
    void Start()
    {
        StartCoroutine(GenerateIntro());
    }

    IEnumerator GenerateIntro()
    {
        string statement = "Generate the introduction of a RPG story.";
        yield return GenerateStory(statement, (story) =>
        {
            uiManager.SetStory(story);
            uiManager.SetCharacterStats(CleanJsonString(characterJson));
            StartCoroutine(GenerateSummaryAndImage(story));
            currentPhase = StoryPhase.AfterIntro;
            uiManager.ShowContinueButton(() =>
            {
                StartCoroutine(GenerateChoices());
            });
        });
    }

    IEnumerator GenerateChoices()
    {
        string statement = "Generate a scenario for my character and give me 3 choices of what he could do next.\n";

        string prereq = $"<History>\n{history}\n</History>\n<Character>\n{characterJson}\n</Character>\nBased on the previously defined history and character, do the following steps:\n";

        string postreq =
            @"Please write out the result in the following format:
<Story>
story
</Story>
<Choice1>
first choice
</Choice1>
<Choice2>
second choice
</Choice2>
<Choice3>
third choice
</Choice3>

Only generate these things in that exact format.";

        string fullPrompt = prereq + statement + postreq;
        yield return SendPost("/chat", fullPrompt, (responseText) =>
        {
            string story = ExtractTag(responseText, "Story");
            lastChoices = new string[]
            {
                ExtractTag(responseText, "Choice1"),
                ExtractTag(responseText, "Choice2"),
                ExtractTag(responseText, "Choice3")
            };
            uiManager.SetStory(story);
            uiManager.SetCharacterStats(CleanJsonString(characterJson));
            uiManager.SetChoices(lastChoices, new Action<int>[] { OnChoice, OnChoice, OnChoice });
            StartCoroutine(GenerateSummaryAndImage(story));
        });
    }

    void OnChoice(int index)
    {
        string choice = lastChoices[index];
        StartCoroutine(DevelopChoice(choice));
    }

    IEnumerator DevelopChoice(string choiceText) {
        string task = $"Continue the story based on the choice {choiceText} and create the resulting character state.";
        string prereq = $"<History>\n{history}\n</History>\n<Character>\n{characterJson}\n</Character>\nBased on the previously defined history and character, do the following steps:\n";
        string postreq = @"Please write out the result in the following format, using the given html-like tags:  
    <Story>  
    story  
    </Story>  
    <Character>  
    {  
       ""health"": resulting health,  
       ""weapon"": resulting weapon,  
       ""armor"": resulting armor,  
       ""gold"": resulting gold,  
       ""items"": [resulting items list]  
    }  
    </Character>  

    Only generate these two things in that exact format.";

        string fullPrompt = prereq + task + postreq;

        while (true) {
            bool isComplete = false;
            yield return SendPost("/chat", fullPrompt, (response) => {
                string story = ExtractTag(response, "Story");
                string character = ExtractTag(response, "Character");
                Debug.Log(response);

                if (!string.IsNullOrEmpty(story) && !string.IsNullOrEmpty(character)) {
                    characterJson = character;
                    history += " " + story;
                    uiManager.SetStory(story);
                    uiManager.SetCharacterStats(CleanJsonString(characterJson));
                    StartCoroutine(GenerateSummaryAndImage(story));
                    currentPhase = StoryPhase.AfterChoice;
                    uiManager.ShowContinueButton(() => {
                        StartCoroutine(GenerateChoices());
                    });
                    isComplete = true;
                }
            });

            if (isComplete)
                break;
        }
    }

    IEnumerator GenerateStory(string statement, Action<string> onComplete)
    {
        string prereq = $"<History>\n{history}\n</History>\n<Character>\n{characterJson}\n</Character>\nBased on the previously defined history and character, do the following steps:\n";

        string postreq = @"Please write out the result in the following format, using the given html-like tags:
<Story>
story
</Story>
<Character>
{
    ""health"": resulting health,
    ""weapon"": resulting weapon,
    ""armor"": resulting armor,
    ""gold"": resulting gold,
    ""items"": [resulting items list]
}
</Character>

Only generate these two things in that exact format.";

        string fullPrompt = prereq + statement + postreq;

        while (true) {
            bool isComplete = false;
            yield return SendPost("/chat", fullPrompt, (responseText) => {
                string story = ExtractTag(responseText, "Story");
                characterJson = ExtractTag(responseText, "Character");
                if (!string.IsNullOrEmpty(story) && !string.IsNullOrEmpty(characterJson)) {
                    history += " " + story;
                    onComplete?.Invoke(story);
                    isComplete = true;
                }
            });
            if (isComplete)
                break;
        }
    }

    IEnumerator ClassifyAndPlayMusic(string story)
    {
        string json = JsonUtility.ToJson(new PromptRequest { prompt = story });
        yield return SendPost("/classify_music", json, (responseText) =>
        {
            MusicResponse music = JsonUtility.FromJson<MusicResponse>(responseText);
            Debug.Log($"Is music null? ({music == null}) Is label empty or null? ({string.IsNullOrEmpty(music.response)})");
            Debug.Log(music);
            if (music != null && !string.IsNullOrEmpty(music.response))
            {
                Debug.Log("Playing music: " + music.response);
                musicManager.PlayMusic(music.response);
            }

        });
    }

    string TrimStoryBySentences(string story, int maxWords) {
        var sentences = System.Text.RegularExpressions.Regex.Split(story.Trim(), @"(?<=[.!?])\s+");
        List<string> trimmed = new List<string>();
        int totalWords = 0;

        for (int i = sentences.Length - 1; i >= 0; i--) {
            string sentence = sentences[i].Trim();
            int wordCount = sentence.Split(' ').Length;

            if (totalWords + wordCount > maxWords)
                break;

            trimmed.Insert(0, sentence);
            totalWords += wordCount;
        }

        return string.Join(" ", trimmed);
    }


    IEnumerator GenerateSummaryAndImage(string story) {
        //      string instruction = "You are a visual prompt generator for an image AI. Convert the following RPG scene into a short, vivid image prompt (max 75 tokens). Focus on characters, action, environment, mood. Do not include unnecessary text.\n\nStory: " + story;

        // inlocuit cu

        Debug.Log(story);

        string trimmedStory = TrimStoryBySentences(story, 150);
        string instruction;

        if (trimmedStory.Split(' ').Length > 60) {
            instruction = "Extract the most visually striking scene from this story as a prompt for an AI image generator." + trimmedStory;
        }
        else {
            instruction = "Extract the most visually striking scene from this story as a prompt for an AI image generator . Describe what is physically happening in one sentence." + trimmedStory;
        }

        //string json = JsonUtility.ToJson(new PromptRequest { prompt = instruction });

        yield return SendPost("/generate_prompt", instruction, (responseText) =>
        {
            Debug.Log(responseText);
            SummaryResponse summary = JsonUtility.FromJson<SummaryResponse>(responseText);
            StartCoroutine(GenerateImage(summary.image_prompt));
            StartCoroutine(ClassifyAndPlayMusic(story));
        });
    }

    IEnumerator GenerateImage(string prompt)
    {
        string postreq = @"";
        string json = JsonUtility.ToJson(new PromptRequest { prompt = prompt + postreq });

        yield return SendPost("/generate_image", json, (responseText) =>
        {
            ImageResponse img = JsonUtility.FromJson<ImageResponse>(responseText);
            byte[] imageBytes = Convert.FromBase64String(img.image_base64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            uiManager.SetImage(tex);
        });
    }

    IEnumerator SendPost(string route, string body, Action<string> onSuccess)
    {
        UnityWebRequest req = new UnityWebRequest(baseUrl + route, "POST");
        string wrappedBody = JsonUtility.ToJson(new PromptRequest { prompt = body });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(wrappedBody);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            Debug.LogError("HTTP Error: " + req.error);
    }

    string ExtractTag(string text, string tag)
    {
        Match match = Regex.Match(text, $"<{tag}>\\s*(.*?)\\s*</{tag}>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim().Replace("\\n", "") : "";
    }
}
