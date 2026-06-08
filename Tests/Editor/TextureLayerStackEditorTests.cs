using System;
using NUnit.Framework;
using Unmanaged.LayeredTexture;
using Unmanaged.LayeredTexture.Editor;
using UnityEditor;
using UnityEngine;

public sealed class TextureLayerStackEditorTests
{
    StackHost host;
    SerializedObject serializedObject;

    [SetUp]
    public void SetUp()
    {
        host = ScriptableObject.CreateInstance<StackHost>();
        serializedObject = new SerializedObject(host);
    }

    [TearDown]
    public void TearDown()
    {
        serializedObject?.Dispose();
        UnityEngine.Object.DestroyImmediate(host);
    }

    [Test]
    public void Constructor_AcceptsLayerStackProperty()
    {
        using var editor = new TextureLayerStackEditor(
            serializedObject,
            serializedObject.FindProperty("First"),
            Context());

        editor.AddLayer(new SolidColorLayer());

        Assert.That(editor.LayerCount, Is.EqualTo(1));
        Assert.That(host.First.Layers, Has.Count.EqualTo(1));
        Assert.That(host.First.Layers[0], Is.TypeOf<SolidColorLayer>());
    }

    [Test]
    public void Constructor_AcceptsLayersProperty()
    {
        using var editor = new TextureLayerStackEditor(
            serializedObject,
            serializedObject.FindProperty("Second").FindPropertyRelative("Layers"),
            Context());

        editor.AddLayer(new NoiseLayer());

        Assert.That(editor.LayerCount, Is.EqualTo(1));
        Assert.That(host.Second.Layers, Has.Count.EqualTo(1));
        Assert.That(host.Second.Layers[0], Is.TypeOf<NoiseLayer>());
    }

    [Test]
    public void Constructor_RejectsInvalidProperty()
    {
        Assert.Throws<ArgumentException>(() => new TextureLayerStackEditor(
            serializedObject,
            serializedObject.FindProperty("Label"),
            Context()));
    }

    [Test]
    public void Editors_ModifyIndependentStacks()
    {
        using var firstEditor = new TextureLayerStackEditor(
            serializedObject,
            serializedObject.FindProperty("First"),
            Context());
        using var secondEditor = new TextureLayerStackEditor(
            serializedObject,
            serializedObject.FindProperty("Second"),
            Context());

        firstEditor.AddLayer(new SolidColorLayer { Color = Color.red });
        firstEditor.DuplicateLayerAt(0);
        secondEditor.AddLayer(new NoiseLayer { Seed = 17 });
        secondEditor.RemoveLayerAt(0);

        Assert.That(host.First.Layers, Has.Count.EqualTo(2));
        Assert.That(host.First.Layers[0], Is.TypeOf<SolidColorLayer>());
        Assert.That(host.First.Layers[1], Is.TypeOf<SolidColorLayer>());
        Assert.That(((SolidColorLayer)host.First.Layers[1]).Color, Is.EqualTo(Color.red));
        Assert.That(host.Second.Layers, Is.Empty);
    }

    TextureLayerStackEditorContext Context() => new()
    {
        UndoTarget = host,
        PreviewRecipe = null,
        GetOutput = () => OutputProfile.Default
    };

    sealed class StackHost : ScriptableObject
    {
        public LayerStack First = new();
        public LayerStack Second = new();
        public string Label;
    }
}
