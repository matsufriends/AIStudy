using System;
using System.Collections.Generic;

[Serializable]
public class ChatGptMessageModel
{
    public string role;
    public string content;
}

//ChatGPT APIにRequestを送るためのJSON用クラス
[Serializable]
public class ChatGptCompletionRequestModel
{
    public string model;
    public List<ChatGptMessageModel> messages;
}

//ChatGPT APIからのResponseを受け取るためのクラス
[Serializable]
public class ChatGptResponseModel
{
    public string id;
    public string @object;
    public int created;
    public Choice[] choices;
    public Usage usage;

    [Serializable]
    public class Choice
    {
        public int index;
        public ChatGptMessageModel message;
        public string finish_reason;
    }

    [Serializable]
    public class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }
}