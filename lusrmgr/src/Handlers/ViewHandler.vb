﻿Class ViewHandler_C
    Private mainF As MainForm

    Public Sub New(mainForm As MainForm)
        mainF = mainForm
    End Sub

    Private currentView As View

    Enum View
        MachineRoot
        BuiltInPrincipals
        Users
        Groups
    End Enum

    Public Function GetView() As View
        Return currentView
    End Function

    Public Sub RefreshMainList()
        ViewChanged(currentView)
    End Sub

    Public Sub RefreshSearch()
        If mainF.SearchWindow IsNot Nothing AndAlso Not mainF.SearchWindow.IsDisposed Then
            mainF.SearchWindow.InitSearch()
        End If
    End Sub

    ''' <summary>
    ''' Refreshes both main list and search.
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub RefreshLists()
        RefreshMainList()
        RefreshSearch()
    End Sub

#Region "Item counters"
    ''' <summary>
    ''' Refreshes the item (selection) count in the status bar.
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub RefreshItemCount()
        'Item counter
        If mainF.list.Items.Count = 1 Then
            mainF.status.Text = "1 element"
        Else
            mainF.status.Text = mainF.list.Items.Count & " elements"
        End If

        UpdateSelectionCounter()
    End Sub

    ''' <summary>
    ''' Updates the status bar indicator showing how many items are selected.
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub UpdateSelectionCounter()
        mainF.itemcount.Text = ""

        If mainF.lselc() = 0 Then Return

        If currentView = View.Users OrElse currentView = View.Groups Then
            mainF.itemcount.Text = "(" & mainF.lselc() & " selected)"
        End If
    End Sub

    Public Sub HideItemCounters()
        mainF.status.Text = ""
        mainF.itemcount.Text = ""
    End Sub
#End Region

#Region "Status bar warnings"

    Private Sub RefreshWarnings()
        Dim Warnings As List(Of ADWarning) = mainF.ADHandler.currentAD().Warnings
        mainF.WarningIndicator.DropDownItems.Clear()

        Select Case Warnings.Count
            Case 0
                mainF.WarningIndicator.Visible = False
                Return
            Case 1
                mainF.WarningIndicator.Visible = True
                mainF.WarningIndicator.Text = "1 Warning"
            Case Else
                mainF.WarningIndicator.Visible = True
                mainF.WarningIndicator.Text = Warnings.Count & " Warnings"
        End Select

        For Each Warning As ADWarning In Warnings
            mainF.WarningIndicator.DropDownItems.Add(Warning.Title, My.Resources.Warning, AddressOf HandleWarningClick).Tag = Warning
        Next
    End Sub

    Private Sub HandleWarningClick(sender As Object, e As EventArgs)
        Dim Warning As ADWarning = DirectCast(sender.Tag, ADWarning)

        Dim tdc As New TASKDIALOGCONFIG
        tdc.cbSize = Runtime.InteropServices.Marshal.SizeOf(tdc)
        tdc.hwndParent = mainF.Handle
        tdc.dwCommonButtons = TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON
        tdc.dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION Or TASKDIALOG_FLAGS.TDF_EXPAND_FOOTER_AREA
        tdc.pszMainIcon = TD_WARNING_ICON

        tdc.pszWindowTitle = "Local users and groups"
        tdc.pszMainInstruction = Warning.Title
        tdc.pszContent = Warning.Description

        If Warning.Details <> "" Then
            tdc.pszExpandedInformation = Warning.Details
            tdc.pszCollapsedControlText = "Show error details"
            tdc.pszExpandedControlText = "Hide error details"
        End If

        TaskDialogIndirect(tdc, 0, 0, 0)
    End Sub
#End Region

    Public Sub ViewChanged(newView As View)
        currentView = newView

        mainF.list.Columns(1).Width = 0
        mainF.list.Columns(0).Width = mainF.list.Width - 4
        mainF.list.MultiSelect = True
        mainF.list.ListViewItemSorter = Nothing
        mainF.list.Items.Clear()
        mainF.QuickSearch.ClearQuickSearch(True)

        Select Case newView
            Case View.Users
                mainF.list.Sorting = SortOrder.Ascending

                mainF.list.Columns(1).Width = mainF.list.Width / 3 - 3
                mainF.list.Columns(0).Width = mainF.list.Width - mainF.list.Columns(1).Width
                mainF.list.Columns(1).Text = "Full name"

                For Each User In mainF.ADHandler.currentAD().UserList
                    mainF.list.Items.Add(New ListViewItem({User.Key, User.Value}, 0)).Name = User.Key
                Next

                RefreshItemCount()

                mainF.PropertyHandler.CanCreateNew = True

            Case View.Groups

                mainF.list.Sorting = SortOrder.Ascending

                For Each Group As String In mainF.ADHandler.currentAD().GroupList
                    mainF.list.Items.Add(Group, Group, 1)
                Next

                RefreshItemCount()

                mainF.PropertyHandler.CanCreateNew = True

            Case View.BuiltInPrincipals

                mainF.PropertyHandler.CanCreateNew = False

                mainF.list.Columns(1).Width = mainF.list.Width / 3 - 3
                mainF.list.Columns(0).Width = mainF.list.Width - mainF.list.Columns(1).Width
                mainF.list.Columns(1).Text = "SID"

                mainF.list.MultiSelect = False
                mainF.list.Sorting = SortOrder.Ascending
                mainF.list.ListViewItemSorter = New ListViewSIDSorter

                For Each Principal As BuiltInPrincipal In mainF.ADHandler.currentAD().BuiltInPrincipals
                    mainF.list.Items.Add(New ListViewItem({Principal.Name, Principal.SID}, 1)).Name = Principal.Name
                Next

                RefreshItemCount()

            Case View.MachineRoot

                mainF.list.MultiSelect = False
                mainF.list.Sorting = SortOrder.None

                If mainF.ADHandler.currentAD() IsNot Nothing AndAlso Not mainF.ADHandler.currentAD().IsLoading() Then
                    mainF.list.Items.Add("Users", 0)
                    mainF.list.Items.Add("Groups", 1)
                    If mainF.ADHandler.currentAD().BuiltInPrincipals.Count > 0 Then
                        mainF.list.Items.Add("Built-in security principals", 5)
                    End If
                    mainF.PropertyHandler.CanCreateNew = True
                Else
                    mainF.PropertyHandler.CanCreateNew = False
                End If

                mainF.tAdd.Enabled = False

                HideItemCounters()
        End Select

        RefreshWarnings()
        mainF.MenuHandler.UpdateMenuControls()
    End Sub

    ''' <summary>
    ''' Compares SID values in the ListView.
    ''' </summary>
    ''' <remarks></remarks>
    Private Class ListViewSIDSorter
        Implements IComparer

        Public Function Compare(x As Object, y As Object) As Integer Implements IComparer.Compare
            Dim sidsplitx As String() = DirectCast(x, ListViewItem).SubItems(1).Text.Split("-"c),
                sidsplity As String() = DirectCast(y, ListViewItem).SubItems(1).Text.Split("-"c)

            If sidsplitx.Length <> sidsplity.Length Then
                For i As Integer = 2 To Math.Max(sidsplitx.Length, sidsplity.Length) - 1
                    If sidsplitx.Length < sidsplity.Length AndAlso i = sidsplitx.Length - 1 Then
                        If sidsplitx(i) = sidsplity(i) Then
                            Return -1
                        End If
                    ElseIf sidsplitx.Length > sidsplity.Length AndAlso i = sidsplity.Length - 1 Then
                        If sidsplitx(i) = sidsplity(i) Then
                            Return 1
                        End If
                    End If

                    If Convert.ToInt32(sidsplitx(i)) < Convert.ToInt32(sidsplity(i)) Then
                        Return -1
                    ElseIf Convert.ToInt32(sidsplitx(i)) > Convert.ToInt32(sidsplity(i)) Then
                        Return 1
                    End If
                Next
            Else
                For i As Integer = 2 To sidsplitx.Length - 1
                    If Convert.ToInt32(sidsplitx(i)) < Convert.ToInt32(sidsplity(i)) Then
                        Return -1
                    ElseIf Convert.ToInt32(sidsplitx(i)) > Convert.ToInt32(sidsplity(i)) Then
                        Return 1
                    End If
                Next
            End If

            Return 0
        End Function
    End Class
End Class
