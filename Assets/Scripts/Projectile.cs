using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 10f;
    private Transform target;

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        if (Vector3.Distance(transform.position, target.position) < 0.5f) // Adjust the collision distance threshold as needed
        {
            target.GetComponent<ExplorerHealth>().TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
