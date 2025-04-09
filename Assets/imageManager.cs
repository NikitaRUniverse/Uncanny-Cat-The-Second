using SUPERCharacter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class imageManager : MonoBehaviour
{
    private ImageSwitcher imageSwitcher;
    private SUPERCharacterAIO character;
    private string lastStatus;
    public AudioClip running;

    void Start()
    { 
        imageSwitcher = GetComponent<ImageSwitcher>();
        character = GetComponent<SUPERCharacterAIO>();
    }

    private void Update()
    {
        if (lastStatus != character.getStatus())
        {
            switch (character.getStatus())
            {
                case "sprint":
                    imageSwitcher.PlayGif("Running", true);
                    GetComponent<AudioSource>().clip = running;
                    GetComponent<AudioSource>().loop = true;
                    GetComponent<AudioSource>().Play();
                    break;

                case "walk":
                    imageSwitcher.PlayGif("Walking", true);
                    GetComponent<AudioSource>().clip = null;
                    GetComponent<AudioSource>().loop = false;
                    break;
            }

            lastStatus = character.getStatus();
        }
    }
}