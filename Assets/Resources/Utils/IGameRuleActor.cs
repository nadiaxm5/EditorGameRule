using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Behavioral contract implemented by every generated actor so the central
// scheduler can evaluate its rules in a fixed, deterministic order.
public interface IGameRuleActor
{
    void EvalUpdate();
    void EvalFixedUpdate();
}

// Central scheduler for generated GameRule actors. Initial actors follow the
// descriptor declaration order; runtime-spawned actors are appended in spawn
// order. Adds and removes are applied only after the current scheduler pass.
public static class ActorScheduler
{
    private struct Entry
    {
        public IGameRuleActor Actor;
        public MonoBehaviour Behaviour;
    }

    private static readonly List<Entry> actors = new List<Entry>();
    private static readonly List<Entry> pendingAdds = new List<Entry>();
    private static readonly List<IGameRuleActor> pendingRemoves = new List<IGameRuleActor>();

    public static void Build(string[] declarationOrder)
    {
        actors.Clear();
        pendingAdds.Clear();
        pendingRemoves.Clear();

        if (declarationOrder == null) return;

        foreach (string actorName in declarationOrder)
        {
            GameObject actorObject = FindSceneObjectByName(actorName);
            if (actorObject == null) continue;

            IGameRuleActor actor = actorObject.GetComponent<IGameRuleActor>();
            if (actor == null) continue;

            actors.Add(new Entry
            {
                Actor = actor,
                Behaviour = actor as MonoBehaviour
            });
        }
    }

    public static void RegisterSpawned(IGameRuleActor actor)
    {
        if (actor == null) return;
        pendingAdds.Add(new Entry
        {
            Actor = actor,
            Behaviour = actor as MonoBehaviour
        });
    }

    public static void Unregister(IGameRuleActor actor)
    {
        if (actor == null) return;
        pendingRemoves.Add(actor);
    }

    public static void RunUpdate()
    {
        int count = actors.Count;
        for (int i = 0; i < count; i++)
        {
            Entry entry = actors[i];
            if (entry.Behaviour != null && entry.Behaviour.isActiveAndEnabled)
                entry.Actor.EvalUpdate();
        }
        Flush();
    }

    public static void RunFixedUpdate()
    {
        int count = actors.Count;
        for (int i = 0; i < count; i++)
        {
            Entry entry = actors[i];
            if (entry.Behaviour != null && entry.Behaviour.isActiveAndEnabled)
                entry.Actor.EvalFixedUpdate();
        }
        Flush();
    }

    private static void Flush()
    {
        if (pendingRemoves.Count > 0)
        {
            actors.RemoveAll(entry =>
                entry.Behaviour == null || pendingRemoves.Contains(entry.Actor));
            pendingRemoves.Clear();
        }

        if (pendingAdds.Count > 0)
        {
            actors.AddRange(pendingAdds);
            pendingAdds.Clear();
        }
    }

    // Unlike GameObject.Find, this also finds initially inactive actors. They
    // stay registered and are skipped until they become active.
    private static GameObject FindSceneObjectByName(string objectName)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name == objectName)
                        return candidate.gameObject;
                }
            }
        }

        return null;
    }
}
