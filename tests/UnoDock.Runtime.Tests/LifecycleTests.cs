using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace UnoDock.Testing;
using LayoutPanel = Xceed.Wpf.AvalonDock.Layout.LayoutPanel;

public static class LifecycleTests
{
    public static async Task<int> Run(DockingManager host,string output)
    {
        var tests=new TestRunner();
        tests.Test("recursive model close emits one event pair",()=>
        {
            using var m=Workspace(out var d);int before=0,after=0;
            d.Closing+=(_,_)=>{before++;d.Close();};d.Closed+=(_,_)=>after++;
            d.Close();Check.Equal(1,before);Check.Equal(1,after);Check.True(d.Parent==null);
        });
        tests.Test("close callback disabling capability preserves model",()=>
        {
            using var m=Workspace(out var d);var parent=d.Parent;d.Closing+=(_,_)=>d.CanClose=false;d.Close();Check.Same(parent,d.Parent);
        });
        tests.Test("close callback reparenting preserves new owner",()=>
        {
            using var m=Workspace(out var d);var target=new LayoutDocumentPane();m.Layout.RootPanel.Children.Add(target);
            d.Closing+=(_,_)=>target.Children.Add(d);d.Close();Check.Same(target,d.Parent);
        });
        tests.Test("manager close callback replaces root safely",()=>
        {
            using var m=Workspace(out var d);var parent=d.Parent;m.DocumentClosing+=(_,_)=>m.Layout=new();d.Close();Check.Same(parent,d.Parent);
        });
        tests.Test("recursive hide emits one event pair",()=>
        {
            using var m=new DockingManager();var tool=new LayoutAnchorable();m.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(tool));
            int before=0,after=0;tool.Hiding+=(_,_)=>{before++;tool.Hide();};tool.Hidden+=(_,_)=>after++;tool.Hide();
            Check.Equal(1,before);Check.Equal(1,after);Check.True(tool.IsHidden);
        });
        tests.Test("hide callback replaces root safely",()=>
        {
            using var m=new DockingManager();var tool=new LayoutAnchorable();m.Layout.RootPanel.Children.Add(new LayoutAnchorablePane(tool));
            var parent=tool.Parent;tool.Hiding+=(_,_)=>m.Layout=new();tool.Hide();Check.Same(parent,tool.Parent);
        });
        tests.Test("recursive float preview runs once",()=>
        {
            using var m=Workspace(out var d);int count=0;m.PreviewFloat+=(_,_)=>{count++;d.Float();};d.Float();Check.Equal(1,count);Check.True(d.IsFloating);
        });
        tests.Test("preview disables floating before commit",()=>
        {
            using var m=Workspace(out var d);m.PreviewFloat+=(_,_)=>d.CanFloat=false;d.Float();Check.False(d.IsFloating);
        });
        tests.Test("preview replaces root before commit",()=>
        {
            using var m=Workspace(out var d);var parent=d.Parent;m.PreviewFloat+=(_,_)=>m.Layout=new();d.Float();Check.Same(parent,d.Parent);
        });
        tests.Test("recursive dock preview runs once",()=>
        {
            using var m=Workspace(out var d);d.Float();int count=0;m.PreviewDock+=(_,_)=>{count++;d.Dock();};d.Dock();Check.Equal(1,count);Check.False(d.IsFloating);
        });
        tests.Test("floating closing hook cancels and suppresses recursive close",()=>
        {
            using var m=Workspace(out var d);d.Float();var window=new FloatProbe(d.FindParent<LayoutDocumentFloatingWindow>()!);
            window.Close();Check.Equal(1,window.ClosingCount);Check.True(window.WasUser);Check.True(d.IsFloating);
            window.ChangeDragging(true);window.ChangeDragging(false);Check.Equal(2,window.DragCount);
        });
        tests.Test("manager DP virtual hook invokes default reconciliation",()=>
        {
            using var m=new ManagerProbe();m.DocumentsSource=new[]{new object()};Check.Equal(1,m.SourceChanges);Check.Equal(1,m.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("item DP virtual hook is dispatched",()=>
        {
            using var item=new ItemProbe();item.Title="changed";Check.Equal(1,item.TitleChanges);
        });
        tests.Test("source callback adds an item reentrantly",()=>
        {
            using var m=new DockingManager();var source=new ObservableCollection<object>();var added=new object();var once=false;
            m.LayoutUpdateStrategy=new Strategy{Before=(_,_,_)=>{if(!once){once=true;source.Add(added);}return false;}};
            m.DocumentsSource=source;source.Add(new object());Check.Equal(2,m.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("source callback removes an item reentrantly",()=>
        {
            using var m=new DockingManager();var source=new ObservableCollection<object>();var first=new object();
            m.LayoutUpdateStrategy=new Strategy{After=(_,d)=>{if(ReferenceEquals(d.Content,first))source.Remove(first);}};
            m.DocumentsSource=source;source.Add(first);Check.Equal(0,m.Layout.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("source callback replaces root without stale insertion",()=>
        {
            using var m=new DockingManager();var old=m.Layout;bool once=false;
            m.LayoutUpdateStrategy=new Strategy{Before=(_,_,_)=>{if(!once){once=true;m.Layout=new();}return false;}};
            m.DocumentsSource=new[]{new object(),new object()};Check.Equal(2,m.Layout.Descendents().OfType<LayoutDocument>().Count());Check.Equal(0,old.Descendents().OfType<LayoutDocument>().Count());
        });
        tests.Test("both sources are snapshotted before mutation",()=>
        {
            using var m=new DockingManager();var value=new object();m.DocumentsSource=new[]{value};
            var batch=m.BeginLayoutUpdate();m.DocumentsSource=Array.Empty<object>();m.AnchorablesSource=Broken();
            Check.Throws<InvalidOperationException>(batch.Dispose);Check.Same(value,m.Layout.Descendents().OfType<LayoutDocument>().Single().Content);
        });
        tests.Test("view access is lazy without rendering",()=>
        {
            using var m=Workspace(out var d);var item=m.GetLayoutItemFromModel(d);Check.False(item.IsViewCreated);
            _=m.LogicalChildrenPublic;Check.Equal(0,m.RealizedContentCount);var view=item.View;Check.True(item.IsViewCreated);Check.Same(view,item.View);
        });
        tests.Test("128 tabs realize only visited editors",()=>
        {
            var docs=Enumerable.Range(0,128).Select(i=>new LayoutDocument{ContentId=i.ToString(),Title="Tab "+i,Content=new object()}).ToArray();
            var pane=new LayoutDocumentPane();foreach(var d in docs)pane.Children.Add(d);
            host.Layout=new(){RootPanel=new LayoutPanel(pane)};host.Refresh();host.UpdateLayout();Check.Equal(1,host.RealizedContentCount);
            var first=host.GetLayoutItemFromModel(docs[0]).View;docs[3].IsActive=true;host.Refresh();Check.Equal(2,host.RealizedContentCount);
            docs[0].IsActive=true;host.Refresh();Check.Same(first,host.GetLayoutItemFromModel(docs[0]).View);
        });
        tests.Test("closed editor releases presenter references",()=>
        {
            using var m=Workspace(out var d);d.Content=new object();var item=m.GetLayoutItemFromModel(d);var view=item.View;
            item.Dispose();Check.True(view.Content==null && view.DataContext==null && view.ContentTemplate==null);Check.False(item.IsViewCreated);
        });
        var original=host.Layout;
        try{return await tests.Run(output,"lifecycle");}finally{host.Layout=original;host.Refresh();}
    }
    private static IEnumerable Broken(){yield return new object();throw new InvalidOperationException("source failure");}
    private static DockingManager Workspace(out LayoutDocument document)
    {document=new(){ContentId="d"};return new(){Layout=new(){RootPanel=new LayoutPanel(new LayoutDocumentPane(document))}};}
    private sealed class ManagerProbe:DockingManager
    {public int SourceChanges;protected override void OnDocumentsSourceChanged(DependencyPropertyChangedEventArgs e){SourceChanges++;base.OnDocumentsSourceChanged(e);}}
    private sealed class ItemProbe:LayoutDocumentItem
    {public int TitleChanges;protected override void OnTitleChanged(DependencyPropertyChangedEventArgs e){TitleChanges++;base.OnTitleChanged(e);}}
    private sealed class FloatProbe(LayoutDocumentFloatingWindow model):LayoutDocumentFloatingWindowControl(model)
    {
        public int ClosingCount,DragCount;public bool WasUser;
        public void ChangeDragging(bool value)=>SetIsDragging(value);
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e){ClosingCount++;WasUser=CloseInitiatedByUser;Close();e.Cancel=true;}
        protected override void OnIsDraggingChanged(DependencyPropertyChangedEventArgs e){DragCount++;base.OnIsDraggingChanged(e);}
    }
    private sealed class Strategy:ILayoutUpdateStrategy
    {
        public Func<LayoutRoot,LayoutDocument,ILayoutContainer,bool>? Before;
        public Action<LayoutRoot,LayoutDocument>? After;
        public bool BeforeInsertDocument(LayoutRoot layout,LayoutDocument documentToShow,ILayoutContainer destinationContainer)=>Before?.Invoke(layout,documentToShow,destinationContainer)??false;
        public void AfterInsertDocument(LayoutRoot layout,LayoutDocument documentShown)=>After?.Invoke(layout,documentShown);
        public bool BeforeInsertAnchorable(LayoutRoot layout,LayoutAnchorable anchorableToShow,ILayoutContainer destinationContainer)=>false;
        public void AfterInsertAnchorable(LayoutRoot layout,LayoutAnchorable anchorableShown){}
    }
}
