﻿using System;
using System.Threading.Tasks;
using EduCATS.Data;
using EduCATS.Helpers.Forms;
using EduCATS.Helpers.Forms.Pages;
using EduCATS.Helpers.Logs;
using Nyxbull.Plugins.CrossLocalization;
using Xamarin.Forms;

namespace EduCATS.Pages.Parental.FindGroup.ViewModels
{  
	/// <summary>
	/// Login page ViewModel.
	/// </summary>
	public class FindGroupPageViewModel : ViewModel
	{
		/// <summary>
		/// Platform services.
		/// </summary>
		readonly IPlatformServices _service;

		/// <summary>
		/// FindGroup page ViewModel constructor.
		/// </summary>
		public FindGroupPageViewModel(IPlatformServices services)
		{
			_service = services;
			IsLoadingCompleted = true;
		}

		bool _isLoading;

		/// <summary>
		/// Property for checking loading status.
		/// </summary>
		public bool IsLoading
		{
			get { return _isLoading; }
			set { SetProperty(ref _isLoading, value); }
		}

		bool _isLoadingCompleted;

		/// <summary>
		/// Property for checking loading status completion.
		/// </summary>
		public bool IsLoadingCompleted
		{
			get { return _isLoadingCompleted; }
			set { SetProperty(ref _isLoadingCompleted, value); }
		}

		string _groupNumber;

		/// <summary>
		/// Group Number property.
		/// </summary>
		public string GroupNumber
		{
			get { return _groupNumber; }
			set { SetProperty(ref _groupNumber, value); }
		}

		string _FIO;

		/// <summary>
		/// FIO property.
		/// </summary>
		public string FIO
		{
			get { return _FIO; }
			set { SetProperty(ref _FIO, value); }
		}

		Command _parentalCommand;
		/// <summary>
		/// Open Main Statistic page
		/// </summary>
		public Command ParentalCommand
		{
			get
			{
				return _parentalCommand ?? (_parentalCommand = new Command(
					async () => await openParental()));
			}
		}

		protected async Task openParental()
		{
			if (string.IsNullOrEmpty(GroupNumber))
			{
				_service.Dialogs.ShowError("Пожалуйста введите номер группы");
				return;
			}
			try
			{
				var result = await DataAccess.GetGroupInfo(GroupNumber);
				if (result.Code.Equals("200"))
				{
					_service.Preferences.GroupId = result.GroupId;
					_service.Preferences.GroupName = GroupNumber;
					_service.Preferences.ChosenGroupId = result.GroupId;
					(_service.Navigation as AppPages).OpenParentalStat(_service, result);
				}
				else
				{
					_service.Dialogs.ShowError("Указанная группа не найдена");
				}
			}
			catch
			{
				_service.Dialogs.ShowError("Не удалось подключиться к серверу");
			}
		}

		Command _settingsCommand;

		/// <summary>
		/// Settigns command.
		/// </summary>
		public Command SettingsCommand
		{
			get
			{
				return _settingsCommand ?? (_settingsCommand = new Command(
					async () => await openSettings()));
			}
		}

		protected async Task openSettings()
		{
			try
			{
				await _service.Navigation.OpenSettings(
					CrossLocalization.Translate("main_settings"));
			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}
	}
}
