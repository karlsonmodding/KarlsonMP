using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class GuiWindow
    {
        private static int widc = 0;

        public GuiWindow(string _title, int x, int y, int width, int height, Action _content, object _storage = null, bool _show = false)
        {
            title = _title;
            rect = new Rect(x, y, width, height);
            content = _content;
            storage = _storage;
            show = _show;
            wid = widc++;
        }

        public object storage;

        private int wid;
        private string title;
        private Rect rect;
        private Action content;

        public void draw()
        {
            if (!show) return;
            rect = GUI.Window(wid, rect, (_) => {
                content();
                GUI.DragWindow();
            }, title);
        }
        public bool show;
    }

    public class GuiSliderAndTextbox
    {
        public GuiSliderAndTextbox(Action<float> targetField, float originalValue, float min, float max, Rect sliderPos, Rect textboxPos)
        {
            this.targetField = targetField;
            this.min = min;
            this.max = max;
            this.sliderPos = sliderPos;
            this.textboxPos = textboxPos;
            value = originalValue;
            textboxValue = value.ToString();
        }

        private Action<float> targetField;
        private float value, min, max;
        private Rect sliderPos, textboxPos;
        string textboxValue;

        public void draw()
        {
            float oldValue = value;
            value = GUI.HorizontalSlider(sliderPos, value, min, max);
            if (value != oldValue)
            {
                targetField(value);
                textboxValue = value.ToString("0.00");
            }
            string oldTextboxValue = textboxValue;
            textboxValue = GUI.TextField(textboxPos, textboxValue);
            if (textboxValue != oldTextboxValue)
            {
                value = float.Parse(textboxValue);
                targetField(value);
            }
        }
    }

    public class GuiSwitch
    {
        public GuiSwitch(Action<bool> targetField, bool originalValue, Rect pos, string text1, string text2)
        {
            this.targetField = targetField;
            this.value = originalValue;
            this.pos = pos;
            options = new string[] { text1, text2 };
        }

        private Action<bool> targetField;
        private bool value;
        private Rect pos;
        private string[] options;

        public void draw()
        {
            bool oldValue = value;
            value = GUI.Toolbar(pos, value ? 0 : 1, options) == 0;
            if (value != oldValue)
                targetField(value);
        }
    }

    public class GuiReflectionCheckbox
    {
        public GuiReflectionCheckbox(FieldInfo field, object field_instance, Rect pos, string text)
        {
            this.field = field;
            this.field_instance = field_instance;
            this.pos = pos;
            this.text = text;
            value = (bool)field.GetValue(field_instance);
        }

        private FieldInfo field;
        private object field_instance;
        private Rect pos;
        private string text;
        private bool value;

        public void draw()
        {
            bool oldValue = value;
            value = GUI.Toggle(pos, value, text);
            if (value != oldValue)
                field.SetValue(field_instance, value);
        }
    }
}
