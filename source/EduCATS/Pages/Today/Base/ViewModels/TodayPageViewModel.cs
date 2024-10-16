﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EduCATS.Data;
using EduCATS.Data.Models;
using EduCATS.Helpers.Date;
using EduCATS.Helpers.Date.Enums;
using EduCATS.Helpers.Forms;
using EduCATS.Helpers.Logs;
using EduCATS.Networking;
using EduCATS.Networking.AppServices;
using EduCATS.Pages.Today.Base.Models;
using EduCATS.Themes;
using Newtonsoft.Json.Linq;
using Nyxbull.Plugins.CrossLocalization;
using Xamarin.Essentials;
using Xamarin.Forms;
using EduCATS.Networking.AppServices;
using EduCATS.Data.User;
using EduCATS.Demo;

namespace EduCATS.Pages.Today.Base.ViewModels
{
	public class TodayPageViewModel : ViewModel
	{
		readonly IPlatformServices _services;

		readonly double _subjectHeight;
		readonly double _subjectsHeightToSubtract;
		readonly bool _isLargeFont;

		const int _minimumCalendarPosition = 0;
		const int _maximumCalendarPosition = 2;
		const double _emptySubjectsHeight = 120;
		const double _emptySubjectsHeightLarge = 130;

		// bool _isCreation = true;
		bool _isManualSelectedCalendarDay;
		DateTime _manualSelectedCalendarDay;
		List<CalendarSubjectsModel> _calendarSubjectsBackup;

		public TodayPageViewModel(double subjectHeight, double subjectsHeaderHeight, IPlatformServices services)
		{
			_subjectHeight = services.Preferences.IsLargeFont ? (subjectHeight + 90) : subjectHeight;
			_subjectsHeightToSubtract = services.Preferences.IsLargeFont ? 95 : 85;
			_isLargeFont = services.Preferences.IsLargeFont;
			_services = services;
			Version = _services.Device.GetVersion();

			initSetup();
			update();
		}

		int _calendarPosition;
		public int CalendarPosition
		{
			get { return _calendarPosition; }
			set { SetProperty(ref _calendarPosition, value); }
		}

		ObservableCollection<CalendarViewModel> _calendarList;
		public ObservableCollection<CalendarViewModel> CalendarList
		{
			get { return _calendarList; }
			set { SetProperty(ref _calendarList, value); }
		}

		ObservableCollection<string> _calendarDaysOfWeekList;
		public ObservableCollection<string> CalendarDaysOfWeekList
		{
			get { return _calendarDaysOfWeekList; }
			set { SetProperty(ref _calendarDaysOfWeekList, value); }
		}

		List<NewsPageModel> _newsList;
		public List<NewsPageModel> NewsList
		{
			get { return _newsList; }
			set { SetProperty(ref _newsList, value); }
		}

		List<SubjectPageModel> _newsSubjectList;
		public List<SubjectPageModel> NewsSubjectList
		{
			get { return _newsSubjectList; }
			set { SetProperty(ref _newsSubjectList, value); }
		}

		List<CalendarSubjectsModel> _calendarSubjects;
		public List<CalendarSubjectsModel> CalendarSubjects
		{
			get { return _calendarSubjects; }
			set { SetProperty(ref _calendarSubjects, value); }
		}

		double _calendarSubjectsHeight;
		public double CalendarSubjectsHeight
		{
			get { return _calendarSubjectsHeight; }
			set { SetProperty(ref _calendarSubjectsHeight, value); }
		}

		string _month;
		public string Month
		{
			get { return _month; }
			set { SetProperty(ref _month, value); }
		}

		bool _isCalendarRefreshing;
		public bool IsCalendarRefreshing
		{
			get { return _isCalendarRefreshing; }
			set { SetProperty(ref _isCalendarRefreshing, value); }
		}

		bool _isNewsRefreshing;
		public bool IsNewsRefreshing
		{
			get { return _isNewsRefreshing; }
			set { SetProperty(ref _isNewsRefreshing, value); }
		}

		bool _isNewsRefreshed;
		public bool IsNewsRefreshed
		{
			get { return _isNewsRefreshed; }
			set { SetProperty(ref _isNewsRefreshed, value); }
		}

		object _selectedNewsItem;
		public object SelectedNewsItem
		{
			get { return _selectedNewsItem; }
			set
			{
				SetProperty(ref _selectedNewsItem, value);

				if (_selectedNewsItem != null)
				{
					openDetailsPage(_selectedNewsItem);
				}
			}
		}
		string _version;
		public string Version
		{
			get { return _version; }
			set { SetProperty(ref _version, value); }
		}

		Command _newsRefreshCommand;
		public Command NewsRefreshCommand
		{
			get
			{
				return _newsRefreshCommand ??
					(_newsRefreshCommand = new Command(update));
			}
		}

		Command<CarouselView.FormsPlugin.Abstractions.PositionSelectedEventArgs> _calendarPositionChangedCommand;
		public Command<CarouselView.FormsPlugin.Abstractions.PositionSelectedEventArgs> PositionSelectedCommandProperty
		{
			get
			{
				return _calendarPositionChangedCommand ?? (
					_calendarPositionChangedCommand = new Command<CarouselView.FormsPlugin.Abstractions.PositionSelectedEventArgs>(
						calendarPositionChangedEvent));
			}
		}

		void initSetup()
		{
			try
			{
				_manualSelectedCalendarDay = new DateTime();
				CalendarSubjects = new List<CalendarSubjectsModel>();
				NewsSubjectList = new List<SubjectPageModel>();
				CalendarDaysOfWeekList = new ObservableCollection<string>(DateHelper.GetDaysWithFirstLetters());
				setInitialCalendarState();
				NewsList = new List<NewsPageModel>();
				_calendarSubjectsBackup = new List<CalendarSubjectsModel>();
			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}

		void setInitialCalendarState()
		{
			var todayDateTime = DateTime.Today;
			var previousWeekCalendarModel = getCalendarViewModel(todayDateTime, WeekEnum.Previous);
			var currentWeekCalendarModel = getCalendarViewModel(todayDateTime, WeekEnum.Current);
			var nextWeekCalendarModel = getCalendarViewModel(todayDateTime, WeekEnum.Next);

			CalendarList = new ObservableCollection<CalendarViewModel> {
				previousWeekCalendarModel,
				currentWeekCalendarModel,
				nextWeekCalendarModel
			};
			CalendarPosition = 1;
			selectCalendarDay(todayDateTime);
		}
		void update()
		{
			_services.Device.MainThread(async () => {
				try
				{
					IsNewsRefreshing = true;
					await getAndSetCalendarNotes();
					await getAndSetNews();
					await getUpdateMessage();
					IsNewsRefreshing = false;
				}
				catch (Exception ex)
				{
					AppLogs.Log(ex);
				}
			});
		}

		async Task getAndSetCalendarNotes()
		{
			if (_services.Preferences.Server == Networking.Servers.EduCatsBntuAddress)
			{
				var calendar = await DataAccess.GetProfileInfoCalendar(_services.Preferences.UserLogin);

				if (calendar == null)
				{
					return;
				}

				var calendarList = new List<CalendarSubjectsModel>();
				var calendarLabsList = calendar.Labs?.Select(c => new CalendarSubjectsModel(c));
				var calendarLectsList = calendar.Lectures?.Select(c => new CalendarSubjectsModel(c));

				if (calendarLabsList == null && calendarLectsList == null)
				{
					return;
				}
				else if (calendarLabsList == null && calendarLectsList != null)
				{
					calendarList = new List<CalendarSubjectsModel>(calendarLectsList);
				}
				else if (calendarLabsList != null && calendarLectsList == null)
				{
					calendarList = new List<CalendarSubjectsModel>(calendarLabsList);
				}
				else
				{
					calendarList = calendarLabsList.Concat(calendarLectsList).ToList();
				}

				_calendarSubjectsBackup = new List<CalendarSubjectsModel>(calendarList);
				setFilteredSubjectsList();
			}
			else
			{
				DateTime date;
				if (_isManualSelectedCalendarDay)
				{
					date = _manualSelectedCalendarDay;
				}
				else
				{
					date = DateTime.Today;
				}

				await setNewSubjectList(date);
			}
		}

		async Task getAndSetNews()
		{
			var news = await getNews();

			if (news != null)
			{
				NewsList = new List<NewsPageModel>(news);
			}
		}

		async Task getUpdateMessage()
		{
			string version = await AppServices.GerVersionStore();
			string[] a = version.Split('.');
			string[] b = _version.Split('.');
			if ((Convert.ToInt32(a[0]) > Convert.ToInt32(b[0])) ||
				(Convert.ToInt32(a[1]) > Convert.ToInt32(b[1]) && Convert.ToInt32(a[0]) == Convert.ToInt32(b[0])) ||
				(Convert.ToInt32(a[2]) > Convert.ToInt32(b[2]) && Convert.ToInt32(a[0]) == Convert.ToInt32(b[0]) &&
				Convert.ToInt32(a[1]) == Convert.ToInt32(b[1])))
			{
				string title = CrossLocalization.Translate("update_title");
				string message = CrossLocalization.Translate("update_message");
				string linkButton = CrossLocalization.Translate("update_link_button");
				string cancelButton = CrossLocalization.Translate("update_cancel_button");

				var result = await _services.Dialogs.ShowMessageUpdate(title, message + version, linkButton, cancelButton);
				if (result)
				{
					if (Device.RuntimePlatform == Device.Android)
						await _services.Device.OpenUri(Servers.EducatsBntuAndroidMarketString);
					else if (Device.RuntimePlatform == Device.iOS)
						await Launcher.OpenAsync(new Uri(Servers.EducatsBntuIOSMarketString));
				}
			}
		}

		async Task<List<NewsPageModel>> getNews()
		{
			var news = await DataAccess.GetNews(_services.Preferences.UserLogin);

			if (DataAccess.IsError && DataAccess.IsSessionExpiredError)
			{
				_services.Dialogs.ShowError(DataAccess.ErrorMessage);
				AppDemo.Instance.IsDemoAccount = false;
				_services.Preferences.ResetPrefs();
				AppUserData.Clear();
				DataAccess.ResetData();
				_services.Navigation.OpenLogin();
				return null;
			}

			if (DataAccess.IsError && !DataAccess.IsConnectionError)
			{
				_services.Dialogs.ShowError(DataAccess.ErrorMessage);
			}

			var subjectList = await getSubjects();

			if (subjectList == null)
			{
				return composeNewsWithSubjects(news, null);
			}

			return composeNewsWithSubjects(news, subjectList.OrderBy(x => x.Name).ToList());
		}

		async Task<IList<SubjectModel>> getSubjects()
		{
			return await DataAccess.GetProfileInfoSubjects(_services.Preferences.UserLogin);
		}

		List<NewsPageModel> composeNewsWithSubjects(IList<NewsModel> news, IList<SubjectModel> subjects)
		{
			if (news == null || subjects == null)
			{
				return null;
			}

			return news.Select(n => {
				var subject = subjects.FirstOrDefault(s => s.Id == n.SubjectId);
				return new NewsPageModel(n, subject?.Color);
			}).ToList();
		}

		void openDetailsPage(object obj)
		{
			try
			{
				var newsPageModel = (NewsPageModel)obj;
				_services.Navigation.OpenNewsDetails(
					newsPageModel.Title,
					newsPageModel.Body);
			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}

		CalendarViewModel getCalendarViewModel(DateTime date, WeekEnum week)
		{
			var weekStartDate = DateHelper.GetWeekStartDate(date, week);
			var weekDates = DateHelper.GetWeekDays(weekStartDate);

			var calendarDaysModelList = weekDates
				.Select(d => new CalendarViewDayModel
				{
					TextColor = Theme.Current.TodayCalendarBaseTextColor,
					Day = d.Day,
					Month = d.Month,
					Year = d.Year
				});

			return new CalendarViewModel(this)
			{
				Days = new ObservableCollection<CalendarViewDayModel>(calendarDaysModelList),
				Month = weekStartDate.Month,
				Year = weekStartDate.Year
			};
		}

		void selectTodayDateWithoutSelectedFlag()
		{
			selectDay(DateTime.Today, false);
		}

		void selectCalendarDay(DateTime dateToSelect)
		{
			selectDay(dateToSelect, true);
		}

		void deselectAllCalendarDays()
		{
			selectDay(deselect: true);
		}

		void selectDay(DateTime dateToCheck = default, bool selected = false, bool deselect = false)
		{
			if (CalendarList == null)
			{
				return;
			}

			foreach (var calendarModel in CalendarList)
			{
				var calendarDayModel = calendarModel.Days.FirstOrDefault(d => {
					return deselect ?
					d.Selected :
					d.Date.ToShortDateString().Equals(dateToCheck.ToShortDateString());

				});

				if (calendarDayModel != null)
				{
					changeCalendarSelection(calendarModel, calendarDayModel, selected);
					break;
				}
			}
		}

		void changeCalendarSelection(
			CalendarViewModel calendarModel,
			CalendarViewDayModel calendarDayModel,
			bool selected)
		{
			try
			{
				var indexCalendarModel = CalendarList.IndexOf(calendarModel);
				var indexCalendarDayModel = calendarModel.Days.IndexOf(calendarDayModel);

				if (DateTime.Today == calendarDayModel.Date)
				{
					calendarDayModel.SelectionColor = Theme.Current.TodaySelectedTodayDateColor;
				}
				else
				{
					if (selected)
					{
						calendarDayModel.SelectionColor = Theme.Current.TodaySelectedAnotherDateColor;
					}
					else
					{
						calendarDayModel.SelectionColor = Theme.Current.TodayNotSelectedDateColor;
					}
				}

				calendarDayModel.Selected = selected;

				if (selected)
				{
					calendarDayModel.TextColor = Theme.Current.TodaySelectedDateTextColor;
				}
				else
				{
					calendarDayModel.TextColor = Theme.Current.TodayNotSelectedDateTextColor;
				}

				var temp = CalendarViewModel.Clone(CalendarList[indexCalendarModel], this);
				temp.Days[indexCalendarDayModel] = calendarDayModel;

				CalendarList[indexCalendarModel] = temp;

			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}

		protected void calendarPositionChangedEvent(CarouselView.FormsPlugin.Abstractions.PositionSelectedEventArgs e)
		{
			try
			{
				selectTodayDateWithoutSelectedFlag();
				deselectAllCalendarDays();

				if (_isManualSelectedCalendarDay)
				{
					selectCalendarDay(_manualSelectedCalendarDay);
				}
				else
				{
					selectCalendarDay(DateTime.Today);
				}

				switch (CalendarPosition)
				{
					case _minimumCalendarPosition:
						getCalendarPosition(_minimumCalendarPosition, WeekEnum.Previous);
						break;
					case _maximumCalendarPosition:
						getCalendarPosition(_maximumCalendarPosition, WeekEnum.Next);
						break;
				}
			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}
		int getCalendarPosition(int boundaryPosition, WeekEnum week)
		{
			int removePosition;
			int calculatedPosition;

			if (boundaryPosition == _maximumCalendarPosition)
			{
				removePosition = _minimumCalendarPosition;
				calculatedPosition = _maximumCalendarPosition - 1;
			}
			else
			{
				removePosition = _maximumCalendarPosition;
				calculatedPosition = _minimumCalendarPosition + 1;
			}

			var date = CalendarList[boundaryPosition].Date;
			var weekViewModel = getCalendarViewModel(date, week);

			CalendarList.RemoveAt(removePosition);
			CalendarList.Insert(boundaryPosition, weekViewModel);

			return calculatedPosition;
		}

		public void ExecuteCalendarSelectionChangedEvent(DateTime date)
		{
			try
			{
				selectTodayDateWithoutSelectedFlag();
				deselectAllCalendarDays();
				_manualSelectedCalendarDay = date;
				_isManualSelectedCalendarDay = true;
				selectCalendarDay(date);

				if (_services.Preferences.Server == Networking.Servers.EduCatsBntuAddress)
				{
					setFilteredSubjectsList();
				}
				else
				{
					_services.Device.MainThread(async () => {
						await setNewSubjectList(date);
					});
				}

			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}

		async Task setNewSubjectList(DateTime dateTime)
		{
			var subjects = await DataAccess.GetSchedule(dateTime.ToString(DateHelper.DateTime.Replace('.', '-')));

			List<SubjectPageModel> temp = subjects.Schedule.Select(n => {
				return new SubjectPageModel(n);
			}).ToList().OrderBy(l => l.Date).ToList();

			NewsSubjectList = temp.OrderBy(x => x.Date).ToList();
			setupNewsSubjectsHeight();
		}


		void setFilteredSubjectsList()
		{
			var filteredList = new List<CalendarSubjectsModel>();

			if (_isManualSelectedCalendarDay)
			{
				filteredList = _calendarSubjectsBackup.Where(
					x => x.Date.ToShortDateString() == _manualSelectedCalendarDay.ToShortDateString()).ToList();
			}
			else
			{
				filteredList = _calendarSubjectsBackup.Where(
					x => x.Date.ToShortDateString() == DateTime.Today.ToShortDateString()).ToList();
			}

			CalendarSubjects = new List<CalendarSubjectsModel>(filteredList);
			setupSubjectsHeight();
		}

		void setupSubjectsHeight()
		{
			try
			{
				if (CalendarSubjects.Count == 0)
				{
					CalendarSubjectsHeight = _emptySubjectsHeight;
					return;
				}

				CalendarSubjectsHeight =
					_subjectHeight * CalendarSubjects.Count;
			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}

		void setupNewsSubjectsHeight()
		{
			try
			{
				if (NewsSubjectList.Count == 0)
				{
					CalendarSubjectsHeight = _isLargeFont ? _emptySubjectsHeightLarge : _emptySubjectsHeight;
					return;
				}

				CalendarSubjectsHeight =
					_subjectHeight * NewsSubjectList.Count - _subjectsHeightToSubtract * (NewsSubjectList.Count - 1);
			}
			catch (Exception ex)
			{
				AppLogs.Log(ex);
			}
		}
	}
}
