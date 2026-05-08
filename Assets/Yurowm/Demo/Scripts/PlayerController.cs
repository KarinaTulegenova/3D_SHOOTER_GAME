using UnityEngine;
using System.Collections;

[RequireComponent (typeof (Animator))]
public class PlayerController : MonoBehaviour {

	public Transform rightGunBone;
	public Transform leftGunBone;
	public Arsenal[] arsenal;
	public bool ApplyArsenalAnimatorController = false;
	public Vector3 rightWeaponLocalPosition = new Vector3(0.04f, -0.02f, 0.035f);
	public Vector3 rightWeaponLocalEulerAngles = new Vector3(-2f, 92f, 1.5f);
	public Vector3 rightWeaponLocalScale = new Vector3(0.3937008f, 0.3937008f, 0.3937008f);

	private Animator animator;
	private Transform currentRightGun;
	private Transform currentLeftGun;
	private Transform rightWeaponHolder;
	private Transform leftWeaponHolder;

	void Awake() {
		ResolveAnimator();
		ResolveWeaponBones();
		if (arsenal.Length > 0)
			SetArsenal (arsenal[0].name);
		}

	public void SetArsenal(string name) {
		ResolveAnimator();
		ResolveWeaponBones();

		if (arsenal == null || arsenal.Length == 0) {
			Debug.LogWarning("[WeaponAttach] No arsenal entries configured for " + name + ".", this);
			return;
		}

		foreach (Arsenal hand in arsenal) {
			if (hand.name == name) {
				ClearWeaponHolder(ref rightWeaponHolder, "WeaponHolder_R");
				ClearWeaponHolder(ref leftWeaponHolder, "WeaponHolder_L");
				if (hand.rightGun != null) {
					if (rightGunBone == null) {
						Debug.LogError("[WeaponAttach] Right gun socket missing. Expected RigPistolRight under RigArmRight3 for arsenal " + name + ".", this);
						return;
					}
					rightWeaponHolder = CreateWeaponHolder(rightGunBone, "WeaponHolder_R");
					if (rightWeaponHolder == null)
						return;
					GameObject newRightGun = (GameObject) Instantiate(hand.rightGun);
					if (newRightGun == null)
						return;
					newRightGun.name = hand.rightGun.name + "_TP";
					newRightGun.transform.SetParent(rightWeaponHolder, false);
					newRightGun.transform.localPosition = rightWeaponLocalPosition;
					newRightGun.transform.localRotation = Quaternion.Euler(rightWeaponLocalEulerAngles);
					newRightGun.transform.localScale = rightWeaponLocalScale;
					currentRightGun = newRightGun.transform;
					Debug.Log("[WeaponAttach] Attached " + newRightGun.name + " to " + GetTransformPath(rightGunBone) +
						"/WeaponHolder_R with configured local weapon offset.", this);
					}
				if (hand.leftGun != null) {
					if (leftGunBone == null) {
						Debug.LogError("[WeaponAttach] Left gun socket missing. Expected RigPistolLeft for arsenal " + name + ".", this);
						return;
					}
					leftWeaponHolder = CreateWeaponHolder(leftGunBone, "WeaponHolder_L");
					if (leftWeaponHolder == null)
						return;
					GameObject newLeftGun = (GameObject) Instantiate(hand.leftGun);
					if (newLeftGun == null)
						return;
					newLeftGun.name = hand.leftGun.name + "_TP";
					newLeftGun.transform.SetParent(leftWeaponHolder, false);
					newLeftGun.transform.localPosition = Vector3.zero;
					newLeftGun.transform.localRotation = Quaternion.identity;
					newLeftGun.transform.localScale = Vector3.one;
					currentLeftGun = newLeftGun.transform;
				}
				if (animator != null && ApplyArsenalAnimatorController && hand.controller != null)
					animator.runtimeAnimatorController = hand.controller;
				return;
				}
		}
	}

	void LateUpdate() {
		ResolveWeaponBones();
		KeepHolderLockedToBone(rightWeaponHolder, rightGunBone);
		KeepHolderLockedToBone(leftWeaponHolder, leftGunBone);
		KeepWeaponLockedToHolder(currentRightGun, rightWeaponHolder);
		KeepWeaponLockedToHolder(currentLeftGun, leftWeaponHolder);
	}

	void ResolveAnimator() {
		if (animator == null)
			animator = GetComponent<Animator> ();

		if (animator == null)
			animator = GetComponentInChildren<Animator> (true);

		if (animator == null)
			Debug.LogError("Animator is missing. Put PlayerController on the object with Animator or assign/use a child Animator.", this);
	}

	void ResolveWeaponBones() {
		Transform preferredRightGunBone = FindChildTransform(transform, "RigPistolRight") ??
			FindChildTransform(transform, "RightHand") ??
			FindChildTransform(transform, "RigArmRight3");
		if (rightGunBone == null || rightGunBone.name != "RigPistolRight")
			rightGunBone = preferredRightGunBone;
		if (rightGunBone != null && rightGunBone.name != "RigPistolRight")
			Debug.LogWarning("[WeaponAttach] RigPistolRight not found. Falling back to " + GetTransformPath(rightGunBone) +
				". Weapon alignment may need manual offset.", this);

		Transform preferredLeftGunBone = FindChildTransform(transform, "RigPistolLeft") ??
			FindChildTransform(transform, "LeftHand") ??
			FindChildTransform(transform, "RigArmLeft3");
		if (leftGunBone == null || leftGunBone.name != "RigPistolLeft")
			leftGunBone = preferredLeftGunBone;
	}

	Transform CreateWeaponHolder(Transform bone, string holderName) {
		Transform existing = bone.Find(holderName);
		if (existing != null)
			Destroy(existing.gameObject);

		GameObject holder = new GameObject(holderName);
		holder.transform.SetParent(bone, false);
		holder.transform.localPosition = Vector3.zero;
		holder.transform.localRotation = Quaternion.identity;
		holder.transform.localScale = Vector3.one;
		return holder.transform;
	}

	void ClearWeaponHolder(ref Transform holder, string holderName) {
		if (holder != null)
			Destroy(holder.gameObject);

		Transform rightExisting = rightGunBone != null ? rightGunBone.Find(holderName) : null;
		if (rightExisting != null)
			Destroy(rightExisting.gameObject);

		Transform leftExisting = leftGunBone != null ? leftGunBone.Find(holderName) : null;
		if (leftExisting != null)
			Destroy(leftExisting.gameObject);

		holder = null;
	}

	void KeepHolderLockedToBone(Transform holder, Transform bone) {
		if (holder == null || bone == null)
			return;

		if (holder.parent != bone)
			holder.SetParent(bone, false);

		holder.localPosition = Vector3.zero;
		holder.localRotation = Quaternion.identity;
		holder.localScale = Vector3.one;
	}

	void KeepWeaponLockedToHolder(Transform weapon, Transform holder) {
		if (weapon == null || holder == null)
			return;

		if (weapon.parent != holder)
			weapon.SetParent(holder, false);

		weapon.localPosition = rightWeaponLocalPosition;
		weapon.localRotation = Quaternion.Euler(rightWeaponLocalEulerAngles);
		weapon.localScale = rightWeaponLocalScale;
	}

	string GetTransformPath(Transform target) {
		if (target == null)
			return "<null>";

		string path = target.name;
		Transform current = target.parent;
		while (current != null) {
			path = current.name + "/" + path;
			current = current.parent;
		}

		return path;
	}

	Transform FindChildTransform(Transform root, string childName) {
		if (root == null)
			return null;

		if (root.name == childName)
			return root;

		for (int i = 0; i < root.childCount; i++) {
			Transform result = FindChildTransform(root.GetChild(i), childName);
			if (result != null)
				return result;
		}

		return null;
	}

	[System.Serializable]
	public struct Arsenal {
		public string name;
		public GameObject rightGun;
		public GameObject leftGun;
		public RuntimeAnimatorController controller;
	}
}
