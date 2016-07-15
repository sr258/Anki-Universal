﻿/*
Copyright (C) 2016 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AnkiU.AnkiCore;
using AnkiU.UIUtilities;
using AnkiU.Models;
using Windows.UI.Text;
using AnkiU.Anki;
using System.Threading.Tasks;
using AnkiU.ViewModels;
using AnkiRuntimeComponent;
using Windows.UI.Popups;
using AnkiU.Interfaces;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Data.Json;

namespace AnkiU.Views
{
    public sealed partial class NoteFieldView : UserControl
    {
        public readonly string HTML_PATH;

        public NoteFieldsViewModel fieldsViewModel;

        private Note currentNote;
        public Note CurrentNote
        {
            get { return currentNote; }
            set
            {
                currentNote = value;
                if (htmlEditor.IsWebviewReady)
                {
                    if (!htmlEditor.IsEditableFieldPopulate)
                    {
                        Task task = PopulateNoteField();
                    }
                    else
                    {
                        Task task = htmlEditor.ChangeAllEditableFieldContent(currentNote.Fields);
                    }

                }
            }
        }

        private string deckMediaFolderName;
        public string DeckMediaFolderName
        {
            get { return deckMediaFolderName; }
            set
            {
                deckMediaFolderName = value;
                if(htmlEditor.IsWebviewReady)
                {
                    Task task = ChangeDeckMediaFolder(deckMediaFolderName);
                }
            }
        }          

        public event ClickEventHandler WebviewButtonClickEvent;
        public event EditableFieldRoutedEventHandler NoteFieldPasteEvent;
        public event NoticeRoutedHandler InitCompleted;

        private MenuFlyout menuFlyout;        
        private HtmlEditor htmlEditor;
        public HtmlEditor HtmlEditor { get { return htmlEditor; } }

        public NoteFieldView()
        {
            this.InitializeComponent();

            if (UIHelper.IsHasPhysicalKeyboard())
                HTML_PATH = "/html/fieldeditor.html";
            else
                HTML_PATH = "/html/fieldeditortouch.html";

            string windowSize = WindowSizeStates.CurrentState.Name;
            menuFlyout = Resources["FieldContextMenu"] as MenuFlyout;

            htmlEditor = new HtmlEditor(webViewGrid, contextMenuPlace, 
                                        menuFlyout, windowSize, HTML_PATH, CoreWindow.GetForCurrentThread().Dispatcher);
            
            htmlEditor.WebviewButtonClickEvent += HtmlEditorWebviewButtonClickEventHandler;
            htmlEditor.EditableFieldPasteEvent += NoteFieldPasteEventHandler;
            htmlEditor.FieldReadyToPopulateEvent += PopulateNoteField;
            htmlEditor.EditableFieldTextChangedEvent += NoteFieldTextChangedEventHandler;
            htmlEditor.FieldPopulateFinishEvent += HtmlEditorFieldPopulateFinishHandler;                 
        }

        private async Task PopulateNoteField()
        {
            //Make sure to change base reference before populating field
            //or else webView can't load media
            await ChangeDeckMediaFolder(deckMediaFolderName);

            var noteFields = new List<NoteField>();
            List<string> fields = new List<string>();
            InitNoteFieldAndFieldString(noteFields, fields);
            await htmlEditor.PopulateAllEditableField(fields);
            fieldsViewModel = new NoteFieldsViewModel(noteFields);
        }

        private void InitNoteFieldAndFieldString(List<NoteField> noteFields, List<string> fields)
        {
            foreach (var f in currentNote.Model["flds"].GetArray())
            {
                string name = f.GetObject().GetNamedString("name");
                int ord = (int)f.GetObject().GetNamedNumber("ord");
                string content = currentNote.GetItem(name);
                fields.Add(name);
                fields.Add(content);

                noteFields.Add(new NoteField(currentNote.Id, name, ord, null));
            }
        }

        private Task HtmlEditorFieldPopulateFinishHandler()
        {
            return Task.Run(() =>
            {
                InitCompleted?.Invoke();
            });
        }

        private void UserControlLoadedHandler(object sender, RoutedEventArgs e)
        {
            htmlEditor.NavigateWebviewToLocalPage();
        }

        private void HtmlEditorWebviewButtonClickEventHandler(object sender)
        {
            WebviewButtonClickEvent?.Invoke(sender);
        }

        private void NoteFieldPasteEventHandler()
        {
            NoteFieldPasteEvent?.Invoke();
        }       

        private void NoteFieldTextChangedEventHandler(string fieldName, string html)
        {  
            currentNote.SetItem(fieldName, html);
        }   

        public async Task AddNewField(string name, Note newNote)
        {
            int count = fieldsViewModel.Fields.Count;
            fieldsViewModel.Fields.Add(new NoteField(currentNote.Id, name, count, null));
            await htmlEditor.InsertNewEditableField(name, "");
            await CreateEditor(name);

            newNote.Tags = currentNote.Tags;
            for(int i = 0; i < count - 1; i++)            
                newNote.SetField(i, currentNote.Fields[i]);

            currentNote = newNote;
        }

        public async Task DeleteField(string name, int order, Note newNote)
        {
            int count = fieldsViewModel.Fields.Count;
            fieldsViewModel.Fields.RemoveAt(order); 

            await RemoveEditor(name);
            await RemoveField(name);

            newNote.Tags = currentNote.Tags;
            for (int i = 0, j = 0; i < count; i++)
            {
                if (i != order)
                {
                    newNote.SetField(j, currentNote.Fields[i]);
                    fieldsViewModel.Fields[j].Order = j;
                    j++;
                }
            }

            currentNote = newNote;
        }

        public async Task RenameField(string name, int order, Note newNote)
        {
            await RenameField(fieldsViewModel.Fields[order].Name, name);
            fieldsViewModel.Fields[order].Name = name;
            newNote.Tags = currentNote.Tags;
            for (int i = 0; i < fieldsViewModel.Fields.Count; i++)            
                newNote.SetField(i, currentNote.Fields[i]);                
            
            currentNote = newNote;
        }

        public async Task MoveField(int oldOrder, int newOrder, Note newNote)
        {
            await MoveField(fieldsViewModel.Fields[oldOrder].Name, newOrder);

            var temp = fieldsViewModel.Fields[oldOrder];
            fieldsViewModel.Fields.RemoveAt(oldOrder);
            fieldsViewModel.Fields.Insert(newOrder, temp);
            for(int i = 0; i < fieldsViewModel.Fields.Count; i++)
                fieldsViewModel.Fields[i].Order = i;
            

            newNote.Tags = currentNote.Tags;
            List<string> fields = new List<string>(currentNote.Fields);
            var fieldMove = fields[oldOrder];
            fields.RemoveAt(oldOrder);
            fields.Insert(newOrder, fieldMove);
            newNote.Fields = fields.ToArray();
            
            currentNote = newNote;
        }

        private async Task CreateEditor(string id)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("CreateEditor", new string[] { id });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task RemoveEditor(string id)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("RemoveEditor", new string[] { id });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task RemoveField(string id)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("RemoveField", new string[] { id });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task RenameField(string id, string name)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("RenameField", new string[] { id, name });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task MoveField(string id, int newOder)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("MoveField", new string[] { id, newOder.ToString() });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }


        public async Task ChangeDeckMediaFolder(string path)
        {
            await htmlEditor.ChangeMediaFolder(path);
        }        

        private void AdaptiveTriggerCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            //WANRING: This will cause memory leak
            //await htmlEditor.LoadNewToolBarWidth(WindowSizeStates.CurrentState.Name);
        }

        private void FieldContextMenuPaste(object sender, RoutedEventArgs e)
        {
            NoteFieldPasteEvent?.Invoke();
        }
    }   
}
