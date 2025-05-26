using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UIManager : MonoBehaviour
{
    public RawImage sceneImage;
    public TextMeshProUGUI storyText;
    public Button[] choiceButtons;
    public Button continueButton;
    public TextMeshProUGUI characterStats;

    public void SetCharacterStats(string stats) {
        characterStats.text = stats;
    }
    public void SetImage(Texture2D texture)
    {
        sceneImage.texture = texture;
    }

    public void SetStory(string story)
    {
        storyText.text = story;
    }

    public void SetChoices(string[] choices, Action<int>[] callbacks)
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = choices[i];
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => callbacks[index]?.Invoke(index));
            choiceButtons[i].gameObject.SetActive(true);
        }
    }

    public void HideChoices()
    {
        foreach (var button in choiceButtons)
            button.gameObject.SetActive(false);
    }

    public void ShowContinueButton(Action callback)
    {
        continueButton.gameObject.SetActive(true);
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() => {
            continueButton.gameObject.SetActive(false);
            callback?.Invoke();
        });
    }
}