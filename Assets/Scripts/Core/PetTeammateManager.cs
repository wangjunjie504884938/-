using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages pet companions and AI teammates lifecycle.
/// Attached to GameManager GameObject.
/// </summary>
public class PetTeammateManager : MonoBehaviour
{
    private readonly List<GameObject> _petObjs = new List<GameObject>();
    private readonly List<GameObject> _teammateObjs = new List<GameObject>();

    public bool IsTeamMode { get; set; } = false;
    public PetCompanion.PetType ActivePetType { get; set; } = PetCompanion.PetType.Spirit;

    private GameManager GM => GameManager.Instance;

    public void CreatePet(Transform playerTransform)
    {
        // 延迟销毁旧宠物，确保渲染线程完成当前帧的GPU资源释放
        foreach (var p in _petObjs) if (p != null) Destroy(p);
        _petObjs.Clear();

        // Only spawn the active pet (one pet at a time)
        var petObj = new GameObject($"Pet_{ActivePetType}");
        petObj.layer = LayerMask.NameToLayer("Player");
        petObj.transform.position = playerTransform.position + new Vector3(-1f, 0f, 0);
        var pet = petObj.AddComponent<PetCompanion>();
        pet.Initialize(playerTransform, GM.DungeonLevel, ActivePetType);
        _petObjs.Add(petObj);
    }

    public void DestroyPets()
    {
        foreach (var p in _petObjs) if (p != null) Destroy(p);
        _petObjs.Clear();
    }

    /// <summary>创建AI队友 — 组队模式下生成2-3个跟随玩家的AI队友</summary>
    public void CreateTeammates(Transform playerTransform, int dungeonLevel)
    {
        foreach (var t in _teammateObjs) if (t != null) Destroy(t);
        _teammateObjs.Clear();

        var teammateDefs = new (string name, HeroClass cls, int level)[]
        {
            ("冰霜法师", HeroClass.Mage, Random.Range(15, 28)),
            ("神圣牧师", HeroClass.Priest, Random.Range(15, 28)),
        };

        for (int i = 0; i < teammateDefs.Length; i++)
        {
            var def = teammateDefs[i];
            var obj = new GameObject($"Teammate_{def.name}");
            obj.layer = LayerMask.NameToLayer("Player");
            obj.transform.position = playerTransform.position + new Vector3(-1f - i * 0.8f, 0, 0);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteCache.WhitePixel;

            var rb = obj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var ai = obj.AddComponent<TeammateAI>();
            ai.Initialize(playerTransform, def.cls, def.level, def.name, dungeonLevel + 1);

            _teammateObjs.Add(obj);
        }
        GameLog.Log($"[Team] 创建了{_teammateObjs.Count}个AI队友");
    }

    /// <summary>销毁所有AI队友</summary>
    public void DestroyTeammates()
    {
        foreach (var t in _teammateObjs) if (t != null) Destroy(t);
        _teammateObjs.Clear();
    }
}
