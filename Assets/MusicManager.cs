using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public AudioSource audioSource;
    private string currentLabel = "";

    public void PlayMusic(string label)
    {
        if (label == currentLabel) return;

        currentLabel = label.ToLower();
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + currentLabel);
        if (clip == null)
        {
            Debug.LogWarning("No audio found for label: " + currentLabel);
            return;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
        Debug.Log("Now playing: " + currentLabel);
    }

    public void StopMusic()
    {
        audioSource.Stop();
        currentLabel = "";
    }
}