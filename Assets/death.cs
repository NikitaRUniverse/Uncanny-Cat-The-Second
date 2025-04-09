using SUPERCharacter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class death : MonoBehaviour
{
    private bool isDead = false;
    public AudioClip deathSound;
    public Image deathImage;
    public GameObject weapons;
    public float restartTime = 5.0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Cat") {
            if (!isDead)
            {
                isDead = true;
                GetComponent<AudioSource>().clip = deathSound;
                GetComponent<AudioSource>().loop = false;
                GetComponent<AudioSource>().Play();
                deathImage.GetComponent<Image>().enabled = true;
                weapons.SetActive(false);
                GetComponentInParent<SUPERCharacterAIO>().enabled = false;
                GetComponentInParent<CapsuleCollider>().enabled = false;
                GetComponentInParent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                StartCoroutine(RestartSceneAfterDelay(restartTime));

            }
        };
    }

    IEnumerator RestartSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
