using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ChatGptConnection
{
    private readonly string _apiKey;
    //会話履歴を保持するリスト
    private readonly List<ChatGptMessageModel> _messageList = new();

    public ChatGptConnection(string apiKey, string prompt = "")
    {
        _apiKey = apiKey;
        _messageList.Add(
            new ChatGptMessageModel()
            {
                role = "system",
                content = prompt,
            });
    }

    public async UniTask<ChatGptResponseModel> RequestAsync(string userMessage)
    {
        //文章生成AIのAPIのエンドポイントを設定
        var apiUrl = "https://api.openai.com/v1/chat/completions";
        _messageList.Add(
            new ChatGptMessageModel
            {
                role = "user",
                content = userMessage
            });

        //OpenAIのAPIリクエストに必要なヘッダー情報を設定
        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer " + _apiKey },
            { "Content-type", "application/json" },
            { "X-Slack-No-Retry", "1" }
        };

        //文章生成で利用するモデルやトークン上限、プロンプトをオプションに設定
        var options = new ChatGptCompletionRequestModel()
        {
            model = "gpt-3.5-turbo",
            messages = _messageList
        };
        var jsonOptions = JsonUtility.ToJson(options);
        Debug.Log("自分:" + userMessage);

        //OpenAIの文章生成(Completion)にAPIリクエストを送り、結果を変数に格納
        using var request = new UnityWebRequest(apiUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonOptions)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        foreach (var header in headers)
        {
            request.SetRequestHeader(header.Key, header.Value);
        }

        await request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(request.error);
            throw new Exception();
        }
        else
        {
            var responseString = request.downloadHandler.text;
            var responseObject = JsonUtility.FromJson<ChatGptResponseModel>(responseString);
            Debug.Log("ChatGPT:" + responseObject.choices[0].message.content);
            _messageList.Add(responseObject.choices[0].message);
            return responseObject;
        }
    }
}