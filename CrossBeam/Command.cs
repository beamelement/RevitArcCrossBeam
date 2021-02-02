using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.DesignScript.Geometry;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit.GeometryConversion;
using Application = Autodesk.Revit.ApplicationServices.Application;
using DG = Autodesk.DesignScript.Geometry;



namespace CrossBeam
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        Result IExternalCommand.Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document revitDoc = commandData.Application.ActiveUIDocument.Document;  //取得文档           
            Application revitApp = commandData.Application.Application;             //取得应用程序            
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;           //取得当前活动文档        

            Document document = commandData.Application.ActiveUIDocument.Document;

            Window1 window1 = new Window1();

            //载入族
            FamilySymbol familySymbol;

            using (Transaction tran = new Transaction(uiDoc.Document))
            {
                tran.Start("载入族");

                //载入弦杆族
                string file = @"C:\Users\zyx\Desktop\2RevitArcBridge\CrossBeam\CrossBeam\source\crossBeam.rfa";
                familySymbol = loadFaimly(file, commandData);
                familySymbol.Activate();

                tran.Commit();
            }


            //把这组模型线通过获取首尾点，生成dynamo里的curve
            List<XYZ> ps = new List<XYZ>();
            using (Transaction transaction = new Transaction(uiDoc.Document))
            {
                transaction.Start("选取模型线生成Curve");

                Selection sel = uiDoc.Selection;
                IList<Reference> modelLines = sel.PickObjects(ObjectType.Element, "选一组模型线");

                foreach (Reference reference in modelLines)
                {
                    Element elem = revitDoc.GetElement(reference);
                    ModelLine modelLine = elem as ModelLine;
                    Autodesk.Revit.DB.Curve c = modelLine.GeometryCurve;

                    ps.Add(c.GetEndPoint(0));
                    ps.Add(c.GetEndPoint(1));

                }

                for (int i = ps.Count - 1; i > 0; i--)
                {
                    XYZ p1 = ps[i];
                    XYZ p2 = ps[i - 1];

                    //注意此处重合点有一个闭合差
                    if (p1.DistanceTo(p2) < 0.0001)
                    {
                        ps.RemoveAt(i);
                    }
                }

                transaction.Commit();
            }


            //做一个revit和dynamo点的转换
            DG.CoordinateSystem coordinateSystem = DG.CoordinateSystem.ByOrigin(0, 0, 0);//标准坐标系
            List<DG.Point> DGps = new List<DG.Point>();
            foreach (XYZ p in ps)
            {

                DGps.Add(p.ToPoint(false));
            }

            DG.PolyCurve polyCurve = DG.PolyCurve.ByPoints(DGps);
            DG.Curve curve = polyCurve as DG.Curve;

            List<DG.Point> DGCBps = new List<DG.Point>();//横梁的放置点位列表
            double StartLength = 0;
            if (window1.ShowDialog() == true)
            {
                //窗口打开并停留，只有点击按键之后，窗口关闭并返回true
            }


            //按键会改变window的属性，通过对属性的循环判断来实现对按键的监测
            while (!window1.Done)
            {

                //选择起点
                if (window1.StartPointSelected)
                {

                    using (Transaction transaction = new Transaction(uiDoc.Document))
                    {
                        transaction.Start("选择起点");

                        double r1 = SelectPoint(commandData);
                        DG.Point dgp1 = curve.PointAtParameter(r1);
                        DGCBps.Add(dgp1);
                        StartLength = curve.SegmentLengthAtParameter(r1);


                        transaction.Commit();
                    }

                    //2、重置window1.StartPointSelected

                    window1.StartPointSelected = false;
                }

                if (window1.ShowDialog() == true)
                {
                    //窗口打开并停留，只有点击按键之后，窗口关闭并返回true
                }

            }



            //在这里根据间距获取到各个点
            for (int i = 1; i < window1.cbNumber; i++)
            {

                double L = StartLength + window1.cbDistance * i;
                DG.Point point = curve.PointAtSegmentLength(L);
                DGCBps.Add(point);
                MessageBox.Show(i.ToString());
            }

            List<FamilyInstance> instances = new List<FamilyInstance>();
            using (Transaction transaction = new Transaction(uiDoc.Document))
            {
                transaction.Start("创建横梁实例");

                foreach(DG.Point p in DGCBps)
                {
                    FamilyInstance familyInstance;
                    familyInstance = CreateFamlyInstance(p, curve, familySymbol, commandData);
                    instances.Add(familyInstance);
                }


                transaction.Commit();
            }


            //给每个族实例设置参数
            using (Transaction transaction = new Transaction(uiDoc.Document))
            {
                transaction.Start("族实例参数设置");

                foreach (FamilyInstance instance in instances)
                {
                    double h1 = instance.LookupParameter("l1/2").AsDouble();
                    instance.LookupParameter("l1/2").Set(window1.l1/2);
                    instance.LookupParameter("l2").Set(window1.l2);

                    //instance.LookupParameter("h1").Set(window1.l1);
                    //instance.LookupParameter("h2").Set(window1.l1);
                }
                transaction.Commit();
            }

            return Result.Succeeded;

        }



        private double SelectPoint(ExternalCommandData commandData)
        {

            Document revitDoc = commandData.Application.ActiveUIDocument.Document;  //取得文档           
            Application revitApp = commandData.Application.Application;             //取得应用程序            
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;           //取得当前活动文档     

            Selection sel = uiDoc.Selection;
            Reference ref1 = sel.PickObject(ObjectType.Element, "选择一条模型线");
            Element elem = revitDoc.GetElement(ref1);
            ModelLine modelLine1 = elem as ModelLine;
            Autodesk.Revit.DB.Curve curve1;
            //做一个判断，判断其是否为ModelNurbSpline
            if (modelLine1 == null)
            {
                ModelNurbSpline modelNurbSpline = elem as ModelNurbSpline;
                curve1 = modelNurbSpline.GeometryCurve;
            }
            else
            {
                curve1 = modelLine1.GeometryCurve;
            }


            Reference ref2 = sel.PickObject(ObjectType.Element, "选择一条模型线");
            elem = revitDoc.GetElement(ref2);
            ModelLine modelLine2 = elem as ModelLine;
            Autodesk.Revit.DB.Curve curve2;
            //做一个判断，判断其是否为ModelNurbSpline
            if (modelLine2 == null)
            {
                ModelNurbSpline modelNurbSpline = elem as ModelNurbSpline;
                curve2 = modelNurbSpline.GeometryCurve;
            }
            else
            {
                curve2 = modelLine2.GeometryCurve;
            }


            //删除第二条选中的线
            uiDoc.Document.Delete(elem.Id);
            //求交点
            curve1.Intersect(curve2, out IntersectionResultArray intersectionResultArray);
            XYZ pointIntersect = intersectionResultArray.get_Item(0).XYZPoint;

            //交点和线都转化为dynamo的点和线
            DG.Point dgP = pointIntersect.ToPoint(false);
            DG.Curve dgC = curve1.ToProtoType(false);
            double ratio = dgC.ParameterAtPoint(dgP);

            return ratio;

        }

        //载入族
        private FamilySymbol loadFaimly(string file, ExternalCommandData commandData)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;           //取得当前活动文档     
            bool loadSuccess = uiDoc.Document.LoadFamily(file, out Family family);

            if (loadSuccess)
            {
                //假如成功导入
                //得到族模板
                ElementId elementId;
                ISet<ElementId> symbols = family.GetFamilySymbolIds();
                elementId = symbols.First();
                FamilySymbol adaptiveFamilySymbol = uiDoc.Document.GetElement(elementId) as FamilySymbol;

                return adaptiveFamilySymbol;
            }
            else
            {
                //假如已经导入,则通过名字找到这个族
                FilteredElementCollector collector = new FilteredElementCollector(uiDoc.Document);
                collector.OfClass(typeof(Family));//过滤得到文档中所有的族
                IList<Element> families = collector.ToElements();
                FamilySymbol adaptiveFamilySymbol = null;
                foreach (Element e in families)
                {

                    Family f = e as Family;
                    //通过名字进行筛选
                    if (f.Name == "crossBeam")
                    {
                        adaptiveFamilySymbol = uiDoc.Document.GetElement(f.GetFamilySymbolIds().First()) as FamilySymbol;
                    }
                }
                return adaptiveFamilySymbol;

            }

        }

        //族实例化,创建族并对其进行旋转
        private FamilyInstance CreateFamlyInstance(DG.Point DGpoint,DG.Curve curve ,FamilySymbol FamilySymbol, ExternalCommandData commandData)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;           //取得当前活动文档    

            XYZ point = DGpoint.ToRevitType(false);//dynamo转Revit

            FamilyInstance familyInstance = uiDoc.Document.Create.NewFamilyInstance(point, FamilySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            LocationPoint locationPoint = familyInstance.Location as LocationPoint;//自适应族基点
            XYZ axisP = new XYZ(point.X, point.Y, point.Z + 100);
            Autodesk.Revit.DB.Line axis = Autodesk.Revit.DB.Line.CreateBound(point, axisP);//旋转轴

            //计算旋转角度
            double ratio = curve.ParameterAtPoint(DGpoint);
            DG.Vector vector = curve.TangentAtParameter(ratio);

            MessageBox.Show(vector.ToString());

            DG.Vector vector1 = DG.Vector.ByCoordinates(vector.X, vector.Y, 0);//三维切向量的平面向量
            DG.Vector vectorY = DG.Vector.ByCoordinates(0, 1, 0);
            double angle = vector1.AngleWithVector(vectorY)/180*Math.PI;
            MessageBox.Show(angle.ToString());
            //locationPoint.Rotate(axis, angle);//旋转横梁
            ElementTransformUtils.RotateElement(uiDoc.Document, familyInstance.Id,axis, -angle);

            return familyInstance;

        }


    }
}
