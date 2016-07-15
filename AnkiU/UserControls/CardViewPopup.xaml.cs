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

using AnkiU.AnkiCore;
using AnkiU.Pages;
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class CardViewPopup : UserControl
    {
        private bool isNightMode = false;

        private const double DEFAULT_HEIGHT_MARGIN = 80;
        private const double DEFAULT_WIDTH_MARGIN = 10;
        private const double STATUS_BAR_PORTRAIT_MARGIN = 20;
        private const double STATUS_BAR_LANDSCAPE_MARGIN = 35;

        private Collection collection;        
        private Card currentCard;

        private double zoomLevel = 1;

        public bool IsLightDismissEnabled { get { return popUp.IsLightDismissEnabled; } set { popUp.IsLightDismissEnabled = value; } }

        public CardViewPopup(Collection collection, long cardId)
        {
            this.InitializeComponent();

            this.collection = collection;
            currentCard = collection.GetCard(cardId);
                                        
            //Hook this CardViewLoadedEvent as soon as we init page
            //To avoid missing it and lead to a white page
            cardView.CardHtmlLoadedEvent += ChangeCardViewContent;            
        }        

        private void CalculateSizeAndPosition()
        {
            var winWidth = CoreWindow.GetForCurrentThread().Bounds.Width;
            var winHeight = CoreWindow.GetForCurrentThread().Bounds.Height;
            var maxWidth = winWidth - DEFAULT_WIDTH_MARGIN;
            var maxHeight = winHeight - DEFAULT_HEIGHT_MARGIN;
            IncludeMobileMarginIfNeeded(ref maxWidth, ref maxHeight);

            popUp.MaxWidth = maxWidth;
            mainGrid.Width = maxWidth;
            popUp.MaxHeight = maxHeight;
            mainGrid.Height = maxHeight;
        }

        private static void IncludeMobileMarginIfNeeded(ref double maxWidth, ref double maxHeight)
        {
            if (UIHelper.IsMobileDevice())
            {
                if (Windows.Graphics.Display.DisplayInformation.GetForCurrentView().CurrentOrientation == Windows.Graphics.Display.DisplayOrientations.Portrait)
                    maxHeight -= STATUS_BAR_PORTRAIT_MARGIN;
                else
                {
                    maxWidth -= STATUS_BAR_LANDSCAPE_MARGIN;
                }
            }
        }

        public void ChangeCard(long cardId)
        {            
            currentCard = collection.GetCard(cardId);
            cardView.InitWebView();
        }

        public void Show()
        {
            CalculateSizeAndPosition();
            popUp.IsOpen = true;
        }

        public void Hide()
        {
            popUp.IsOpen = false;
            //WARNING: Until memory-leak in webview is fixed, we have to clear and init it every time
            cardView.ClearWebViewControl();
        }

        private async void ChangeCardViewContent()
        {
            await ChangeZoomLevel(0);
            await ChangeHtmlheader();            
            await DisplayAnswerSide();            
        }

        private async Task ChangeHtmlheader()
        {
            await ChangeDeckMediaFolder();
            await ChangeCardStyle();
        }

        private async Task ChangeDeckMediaFolder()
        {
            long deckMediaId;
            if (collection.Deck.IsDyn(currentCard.DeckId))
                deckMediaId = currentCard.OriginalDeckId;
            else
                deckMediaId = currentCard.DeckId;
            string deckMediaFolder = "/" + collection.Media.MediaFolder.Name + "/" + deckMediaId + "/";
            await cardView.ChangeDeckMediaFolder(deckMediaFolder);
        }

        private async Task ChangeCardStyle()
        {
            string cardStyle = currentCard.CssWithoutStyleTag();
            await cardView.ChangeCardStyle(cardStyle);
        }

        private async Task DisplayAnswerSide()
        {
            var questionAndAnswer = currentCard.GetQuestionAndAnswer();
            string answer = GetFilterAnswer(questionAndAnswer);
            var cardClass = $"card card{currentCard.Ord + 1}";
            await cardView.ChangeCardContent(answer, cardClass);
        }

        private string GetFilterAnswer(Dictionary<string, string> questionAndAnswer)
        {
            TypeField type = new TypeField();
            ReviewPage.TypeAnsQuestionFilter(questionAndAnswer["q"], currentCard, ref type);
            var answer = Sound.ExpandSounds(questionAndAnswer["a"]);
            answer = LaTeX.MungeQA(answer, collection);
            if (String.IsNullOrWhiteSpace(type.CorrectAnswer))
                answer = ReviewPage.TypeAnswerRegex.Replace(answer, "");
            else
                answer = ReviewPage.TypeAnswerRegex.Replace(answer, type.CorrectAnswer);
            return answer;
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            cardView.ToggleReadMode();
            if (isNightMode)
            {
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.Black);
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.White);                
            }
            else
            {
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.White);
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
            }
        }

        public void Close()
        {
            cardView.ClearWebViewControl();
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private async void ZoomOutButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await ChangeZoomLevel(-MainPage.ZOOM_STEP);
        }
        private async void ZoomInButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await ChangeZoomLevel(MainPage.ZOOM_STEP);
        }
        private async void ZoomResetButtonClickHandler(object sender, RoutedEventArgs e)
        {
            zoomLevel = MainPage.GetDefaultZoomLevel();
            await cardView.ChangeZoomLevel(zoomLevel);
        }
        private async Task ChangeZoomLevel(double delta)
        {
            zoomLevel += delta;            
            await cardView.ChangeZoomLevel(zoomLevel);
        }

        private void UserControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalculateSizeAndPosition();
        }
    }
}
