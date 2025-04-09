using UnityEngine;

public class WeaponTrigger : MonoBehaviour
{
    public bool isShooting = false;
    public GameObject weapons;

    void Update()
    {
        if (isShooting)
        {
            weapons.GetComponent<WeaponSystem>().weapons[weapons.GetComponent<WeaponSystem>().weaponIndex].GetComponent<Weapon>().RemoteFire();
        }
    }
}