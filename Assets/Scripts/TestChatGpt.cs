using System;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public sealed class TestChatGpt : EditorWindow
{
    [MenuItem("Tools/TestChatGpt")]
    private static void ShowWindow()
    {
        GetWindow<TestChatGpt>();
    }

    private string _apiKey;
    private string _systemPrompt;
    private string _userMessage;
    private string _response;
    private State _state = State.Inputting;

    private enum State
    {
        Inputting,
        Requesting,
    }

    // APIキーとプロンプトを設定
    private void OnGUI()
    {
        switch (_state)
        {
            case State.Inputting:
                OnGUIInputtingAsync().Forget();
                break;
            case State.Requesting:
                OnGUIRequesting();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async UniTask OnGUIInputtingAsync()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("API Key");
            _apiKey = EditorGUILayout.TextField(_apiKey);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("System Prompt", GUILayout.Width(96));
            _systemPrompt = EditorGUILayout.TextArea(_systemPrompt, GUILayout.Height(48));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("User Message", GUILayout.Width(96));
            _userMessage = EditorGUILayout.TextArea(_userMessage, GUILayout.Height(48));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Response", GUILayout.Width(96));
            // _responseの内容を変更不可で表示
            // DisabledGroup
            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.TextArea(_response, GUILayout.Height(48));
            }
        }

        if (GUILayout.Button("Send"))
        {
            _state = State.Requesting;
            var connection = new ChatGptConnection(_apiKey, _systemPrompt);
            var result = await connection.RequestAsync(_userMessage);
            _response = result.choices[0].message.content;
            _state = State.Inputting;
            return;
        }

        if (GUILayout.Button("Clear"))
        {
            _apiKey = "";
            _systemPrompt = "";
        }
    }

    private void OnGUIRequesting()
    {
        EditorGUILayout.LabelField("Requesting...");
    }
}