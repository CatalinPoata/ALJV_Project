using System;
using UnityEngine;

public class TestUI : MonoBehaviour
{
    public UIManager uiManager;

    void Start()
    {
        uiManager.SetStory("The knight enters a cave full of glowing symbols.");
        uiManager.SetChoices(new string[] { "Advance", "Look around", "Leave" }, new Action<int>[] {
            (i) => Debug.Log("Chose 1"),
            (i) => Debug.Log("Chose 2"),
            (i) => Debug.Log("Chose 3")
        });
    }
}