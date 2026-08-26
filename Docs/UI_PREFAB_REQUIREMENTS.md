# UI Prefab Requirements

This project should use authored UI prefabs instead of building visual UI in C#.

## Boss Health Panel

Create a prefab at:

`Assets/Resources/UI/BossHealthPanel.prefab`

Required hierarchy/names:

```text
BossHealthPanel
├── BossName      (UnityEngine.UI.Text)
├── BossHpBar     (UnityEngine.UI.Slider)
└── BossHpText    (UnityEngine.UI.Text)
```

Notes:

- The prefab should be designed in the Unity editor.
- `GameUI` only loads/binds this prefab and updates values.
- The panel is hidden by default and shown when a boss is active.
