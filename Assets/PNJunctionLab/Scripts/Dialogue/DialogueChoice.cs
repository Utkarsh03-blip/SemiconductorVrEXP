using System;
using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Represents a branching choice button inside a DialogueLine.
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        [SerializeField] private string label;
        [SerializeField] private DialogueLine nextLine;

        public string Label => label;
        public DialogueLine NextLine => nextLine;

        public void Configure(string choiceLabel, DialogueLine targetLine)
        {
            label = choiceLabel;
            nextLine = targetLine;
        }
    }
}
