using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageSwitcher : MonoBehaviour
{
    public Image imageUI;
    public Sprite defaultSprite;
    public float displayDuration = 5f;
    public float gifFrameTime = 0.1f;

    private Dictionary<string, Sprite[]> gifs = new Dictionary<string, Sprite[]>();
    private Coroutine gifCoroutine;
    private bool isEndlessGifRunning = false;

    private void Start()
    {
        LoadGifFrames("Running");
        LoadGifFrames("Walking");
        LoadGifFrames("Spinning");

        imageUI.sprite = defaultSprite;

        imageUI.rectTransform.sizeDelta = new Vector2(Screen.height / 3, Screen.height / 3);
    }

    private void LoadGifFrames(string gifName)
    {
        Sprite[] frames = Resources.LoadAll<Sprite>(gifName);
        if (frames.Length > 0)
        {
            gifs[gifName] = frames;
        }
    }

    public void PlayGif(string gifName, bool isEndless = false)
    {
        if (isEndlessGifRunning && !isEndless)
        {
            return;
        }

        if (gifCoroutine != null)
        {
            StopCoroutine(gifCoroutine);
        }

        isEndlessGifRunning = isEndless;

        gifCoroutine = StartCoroutine(DisplayGifLoop(gifName, isEndless));
    }

    private IEnumerator DisplayGifLoop(string gifName, bool isEndless)
    {
        Sprite[] frames = gifs[gifName];
        float elapsedTime = 0f;

        while (isEndless || elapsedTime < displayDuration)
        {
            foreach (Sprite frame in frames)
            {
                imageUI.sprite = frame;
                yield return new WaitForSeconds(gifFrameTime);

                if (!isEndless)
                {
                    elapsedTime += gifFrameTime;
                    if (elapsedTime >= displayDuration)
                    {
                        break;
                    }
                }
            }
        }

        if (!isEndless)
        {
            imageUI.sprite = defaultSprite;
            isEndlessGifRunning = false;
        }
    }

    public void StopEndlessGif()
    {
        if (isEndlessGifRunning)
        {
            if (gifCoroutine != null)
            {
                StopCoroutine(gifCoroutine);
                gifCoroutine = null;
            }
            imageUI.sprite = defaultSprite;
            isEndlessGifRunning = false;
        }
    }
}
