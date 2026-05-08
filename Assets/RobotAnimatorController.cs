using UnityEngine;

public class RobotAnimatorController : MonoBehaviour
{
    private Animator animator;

    private Vector3 lastPosition;

    void Start()
    {
        animator = GetComponent<Animator>();

        lastPosition = transform.position;
    }

    void Update()
    {
        float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;

        animator.SetFloat("Speed", speed);

        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger("Shoot");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            animator.SetTrigger("Reload");
        }

        lastPosition = transform.position;
    }
}