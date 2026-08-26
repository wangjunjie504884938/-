using UnityEngine;

/// <summary>
/// Enemy projectile that damages the player on contact, then returns to pool.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    private void OnEnable() => SceneRegistry.Register(this);
    private void OnDisable() { SceneRegistry.Unregister(this); ResetState(); }

    private int damage;
    private Rigidbody2D rb;

    public void Initialize(Vector2 dir, int dmg)
    {
        damage = dmg;
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = dir * 8f;
        StartCoroutine(ReturnAfterLifetime());
    }

    private System.Collections.IEnumerator ReturnAfterLifetime()
    {
        yield return new WaitForSeconds(3f);
        VFXPool.Return("enemy_proj", gameObject);
    }

    private void ResetState()
    {
        StopAllCoroutines();
        damage = 0;
        if (rb != null) rb.velocity = Vector2.zero;
        transform.localScale = Vector3.one;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
                player.TakeDamage(damage);
            VFXPool.Return("enemy_proj", gameObject);
        }
    }
}
