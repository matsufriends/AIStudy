using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class MethodPrediction : EditorWindow
{
    //テキストアセット
    [SerializeField] private TextAsset _methodNamesFile;
    [SerializeField] private TextAsset _bodyTokensFile;
    [SerializeField] private TextAsset _testLabelsFile;
    [SerializeField] private TextAsset _resultFile;
    [SerializeField] private List<string> _methodNames = new List<string>();
    [SerializeField] private List<string> _methodBody = new List<string>();
    [SerializeField] private List<string> _labels = new List<string>();

    [MenuItem("Tools/MethodPrediction")]
    private static void ShowWindow()
    {
        GetWindow<MethodPrediction>();
    }

    private string _apiKey;
    private string _systemPrompt;
    private string _userMessage;
    private int _index;
    private string _request;
    private string _response;
    private State _state = State.Inputting;
    private CancellationTokenSource _source;
    private const int RequestInterval = 5;
    private const int ReRequestSeconds = 10;

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
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Parsed Method Names ({_methodNames.Count} lines)");
                _methodNamesFile = (TextAsset)EditorGUILayout.ObjectField(_methodNamesFile, typeof(TextAsset), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Method Body Tokens ({_methodBody.Count} lines)");
                _bodyTokensFile = (TextAsset)EditorGUILayout.ObjectField(_bodyTokensFile, typeof(TextAsset), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Test Labels ({_labels.Count} lines)");
                _testLabelsFile = (TextAsset)EditorGUILayout.ObjectField(_testLabelsFile, typeof(TextAsset), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Result");
                _resultFile = (TextAsset)EditorGUILayout.ObjectField(_resultFile, typeof(TextAsset), true);
            }

            if (GUILayout.Button("Parse"))
            {
                _methodNames = new List<string>(_methodNamesFile.text.Split('\n'));
                _methodBody = new List<string>(_bodyTokensFile.text.Split('\n'));
                _labels = new List<string>(_testLabelsFile.text.Split('\n'));
            }
        }
        GUILayout.Space(16);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("API Key");
            _apiKey = EditorGUILayout.TextField(_apiKey);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("System Prompt", GUILayout.Width(96));
            _systemPrompt = EditorGUILayout.TextArea(_systemPrompt, GUILayout.Height(192));
        }

        GUILayout.Space(16);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Index", GUILayout.Width(96));
            _index = EditorGUILayout.IntField(_index);
        }

        // Index行目のListをそれぞれ表示
        {
            using (new EditorGUI.DisabledGroupScope(true))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Method Name", GUILayout.Width(96));
                    EditorGUILayout.TextArea(_methodNames[_index], GUILayout.Height(48));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Method Body", GUILayout.Width(96));
                    EditorGUILayout.TextArea(_methodBody[_index], GUILayout.Height(48));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Label", GUILayout.Width(96));
                    EditorGUILayout.TextArea(_labels[_index], GUILayout.Height(48));
                }
            }
        }
        GUILayout.Space(16);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("User Message", GUILayout.Width(96));
            _userMessage = EditorGUILayout.TextArea(_userMessage, GUILayout.Height(48));
        }

        if (GUILayout.Button("Send"))
        {
            _state = State.Requesting;
            _source = new CancellationTokenSource();
            await SendAsync(_index);
            _state = State.Inputting;
            Repaint();
            return;
        }

        if (GUILayout.Button("Send10"))
        {
            _state = State.Requesting;
            _source = new CancellationTokenSource();
            for (var i = 0; i < 10; i++)
            {
                await SendAsync(_index);
                await WaitSeconds(RequestInterval, _source.Token);
                if (_state == State.Inputting)
                {
                    return;
                }

                _index++;
            }

            _state = State.Inputting;
            Repaint();
            return;
        }

        GUILayout.Space(16);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Request", GUILayout.Width(96));
            // _responseの内容を変更不可で表示
            // DisabledGroup
            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.TextArea(_request, GUILayout.Height(48));
            }
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
    }

    private async static UniTask WaitSeconds(int seconds, CancellationToken token)
    {
        Debug.Log($"{seconds}秒待機します.");
        for (var leftSeconds = seconds; leftSeconds > 0; leftSeconds--)
        {
            Debug.Log($"{leftSeconds}秒.");
            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
        }
    }

    private async UniTask SendAsync(int index)
    {
        var methodName = _methodNames[index];
        methodName = methodName[(methodName.IndexOf("@", StringComparison.Ordinal) + 1)..];
        var methodNameAfter = methodName.Contains(",") ? methodName[(methodName.IndexOf(",", StringComparison.Ordinal) + 1)..] : "";
        methodName = methodName.Replace(",", "");
        var methodBody = _methodBody[index];
        var connection = new ChatGptConnection(_apiKey, _systemPrompt);
        _request = string.Format(_userMessage, methodNameAfter, methodBody);
        for (var i = 0; i < 5; i++)
        {
            try
            {
                var result = await connection.RequestAsync(_request);
                _response = result.choices[0].message.content;
                break;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                await WaitSeconds(ReRequestSeconds, _source.Token);
                if (_state == State.Inputting)
                {
                    return;
                }
            }
        }

        // _resultのTextAssetに書き込んで保存する
        var path = AssetDatabase.GetAssetPath(_resultFile);
        await File.AppendAllTextAsync(path, $"\n{index},{_labels[index]},{methodName},{_response}");
    }

    private void OnGUIRequesting()
    {
        EditorGUILayout.LabelField("Requesting...");
        if (GUILayout.Button("Force Stop"))
        {
            _source?.Cancel();
            _state = State.Inputting;
        }
    }
}