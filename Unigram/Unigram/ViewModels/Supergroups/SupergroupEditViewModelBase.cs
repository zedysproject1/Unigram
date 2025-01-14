﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public abstract class SupergroupEditViewModelBase : TLViewModelBase
        , IDelegable<ISupergroupEditDelegate>
        , IHandle
        //IHandle<UpdateSupergroup>,
        //IHandle<UpdateSupergroupFullInfo>,
        //IHandle<UpdateBasicGroup>,
        //IHandle<UpdateBasicGroupFullInfo>
    {
        public ISupergroupEditDelegate Delegate { get; set; }

        public SupergroupEditViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            AdminedPublicChannels = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute);
            RevokeLinkCommand = new RelayCommand<Chat>(RevokeLinkExecute);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        protected bool _isPublic = true;
        public bool IsPublic
        {
            get => _isPublic;
            set => Set(ref _isPublic, value);
        }

        protected string _username;
        public string Username
        {
            get => _username;
            set => Set(ref _username, value);
        }

        protected bool _hasTooMuchUsernames;
        public bool HasTooMuchUsernames
        {
            get => _hasTooMuchUsernames;
            set => Set(ref _hasTooMuchUsernames, value);
        }

        protected string _inviteLink;
        public string InviteLink
        {
            get => _inviteLink;
            set => Set(ref _inviteLink, value);
        }

        public MvxObservableCollection<Chat> AdminedPublicChannels { get; private set; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }

                if (string.IsNullOrEmpty(item.Username))
                {
                    LoadUsername(chat.Id);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ClientService.GetBasicGroup(basic.BasicGroupId);
                var cache = ClientService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                }
                else
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }

                LoadUsername(0);
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSupergroup>(this, Handle)
                .Subscribe<UpdateSupergroupFullInfo>(Handle)
                .Subscribe<UpdateBasicGroup>(Handle)
                .Subscribe<UpdateBasicGroupFullInfo>(Handle);
        }

        public void Handle(UpdateSupergroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.Supergroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateSupergroup(chat, update.Supergroup));
            }
        }

        public void Handle(UpdateSupergroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.SupergroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateSupergroupFullInfo(chat, ClientService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
            }
        }

        public void Handle(UpdateBasicGroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroup(chat, update.BasicGroup));
            }
        }

        public void Handle(UpdateBasicGroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, ClientService.GetBasicGroup(update.BasicGroupId), update.BasicGroupFullInfo));
            }
        }



        public void Handle(UpdateChatTitle update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatTitle(_chat));
            }
        }

        public void Handle(UpdateChatPhoto update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPhoto(_chat));
            }
        }



        private async void LoadUsername(long chatId)
        {
            var response = await ClientService.SendAsync(new CheckChatUsername(chatId, "username"));
            if (response is CheckChatUsernameResultPublicChatsTooMuch)
            {
                HasTooMuchUsernames = true;
                LoadAdminedPublicChannels();
            }
            else
            {
                HasTooMuchUsernames = false;
            }
        }

        protected async void LoadAdminedPublicChannels()
        {
            if (AdminedPublicChannels.Count > 0)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetCreatedPublicChats());
            if (response is Telegram.Td.Api.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var id in chats.ChatIds)
                {
                    var chat = ClientService.GetChat(id);
                    if (chat != null)
                    {
                        result.Add(chat);
                    }
                }

                AdminedPublicChannels.ReplaceWith(result);
            }
            else if (response is Error error)
            {
                Logs.Logger.Error(Logs.LogTarget.API, "channels.getAdminedPublicChannels error " + error);
            }
        }

        public RelayCommand SendCommand { get; }
        protected abstract void SendExecute();

        public RelayCommand<Chat> RevokeLinkCommand { get; }
        private async void RevokeLinkExecute(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                var dialog = new MessagePopup();
                dialog.Title = Strings.Resources.AppName;
                dialog.Message = string.Format(Strings.Resources.RevokeLinkAlert, MeUrlPrefixConverter.Convert(ClientService, supergroup.Username, true), chat.Title);
                dialog.PrimaryButtonText = Strings.Resources.RevokeButton;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await ClientService.SendAsync(new SetSupergroupUsername(supergroup.Id, string.Empty));
                    if (response is Ok)
                    {
                        HasTooMuchUsernames = false;
                        AdminedPublicChannels.Clear();
                    }
                }
            }
        }

        #region Username

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            set => Set(ref _isValid, value);
        }

        private bool _isAvailable;
        public bool IsAvailable
        {
            get => _isAvailable;
            set => Set(ref _isAvailable, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => Set(ref _errorMessage, value);
        }

        public async void CheckAvailability(string text)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup != null && string.Equals(text, supergroup.Username))
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = null;

                return;
            }

            var chatId = chat.Type is ChatTypeSupergroup ? chat.Id : 0;

            var response = await ClientService.SendAsync(new CheckChatUsername(chatId, text));
            if (response is CheckChatUsernameResultOk)
            {
                IsLoading = false;
                IsAvailable = true;
                ErrorMessage = null;
            }
            else if (response is CheckChatUsernameResultUsernameInvalid)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = Strings.Resources.UsernameInvalid;
            }
            else if (response is CheckChatUsernameResultUsernameOccupied)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = Strings.Resources.UsernameInUse;
            }
            else if (response is CheckChatUsernameResultPublicChatsTooMuch)
            {
                HasTooMuchUsernames = true;
                LoadAdminedPublicChannels();
            }
            else if (response is Error error)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = error.Message;
            }
        }

        public bool UpdateIsValid(string username)
        {
            IsValid = IsValidUsername(username);
            IsLoading = false;
            IsAvailable = false;

            if (!IsValid)
            {
                if (string.IsNullOrEmpty(username))
                {
                    ErrorMessage = null;
                }
                else if (_username.Length < 5)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidShort;
                }
                else if (_username.Length > 32)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidLong;
                }
                else
                {
                    ErrorMessage = Strings.Resources.UsernameInvalid;
                }
            }
            else
            {
                IsLoading = true;
                ErrorMessage = null;
            }

            return IsValid;
        }

        public bool IsValidUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            if (username.Length < 5)
            {
                return false;
            }

            if (username.Length > 32)
            {
                return false;
            }

            for (int i = 0; i < username.Length; i++)
            {
                if (!MessageHelper.IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
