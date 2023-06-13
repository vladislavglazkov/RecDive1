﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Web.Mvc;
using System.IO;
namespace RecDive1.Controllers
{
    public class ValidateCodeModel
    {
        public string Code { get; set; }
    }

    public class RunCheckModel
    {
        public string Code { get; set; }
        public int Id { get; set; }
        public string[] Args { get; set; }
    }
    public class MainController : Controller
    {
        // GET: Main
        public ActionResult Index()
        {
            return View("~/Views/Shared/RDView.cshtml");
        }

        // GET: Main/Details/5
       

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult ValidateCode(ValidateCodeModel model)
        {
            try
            {
                // TODO: Add update logic here
                var comp=RecDive1.CodeAnalyze.CodeComponent.Load(model.Code);
                var mds=comp.GetAllMethods();
                List<object> lst = new List<object>();
                int id = 0;
                foreach (var method in mds)
                {
                    lst.Add(new { Id=id++,Name=CodeAnalyze.CodeComponent.GetFullMethodSignature(method),Params=method.ParameterList.Parameters.Count});
                }
                return Json(lst);
                
            }
            catch
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult RunCheck()
        {
            string body;
            using (StreamReader reader = new StreamReader(Request.InputStream))
            {
                body= reader.ReadToEnd();
            }
            RunCheckModel model=System.Text.Json.JsonSerializer.Deserialize<RunCheckModel>(body);


            var comp = RecDive1.CodeAnalyze.CodeComponent.Load(model.Code);

            var mds = comp.GetAllMethods();
            List<object> lst = new List<object>();
            var method = mds[model.Id];
            var parentClass = method.AncestorsAndSelf().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
            var i = 0;
            List<object> args = new List<object>();
            foreach (var h in method.ParameterList.Parameters)
            {
                
                var typeRaw=h.Type.GetText().ToString();
                Type type = typeof(int);
                if (typeRaw.Contains("string"))
                {
                    type = typeof(string);
                }
                if (typeRaw.Contains("bool"))
                {
                    type = typeof(bool);
                }
                if (typeRaw.Contains("char"))
                {
                    type = typeof(char);
                }
                args.Add(System.Convert.ChangeType(model.Args[i++], type));
            }

            var ans=comp.GetRes(parentClass.Identifier.Text, method.Identifier.Text,args.ToArray());
            if (ans!=null)
                return Json(new { Answer=$"Maximum recursion depth: {ans}"});
            return Json(new { Answer = "Recursion depth exceeded!" });
        }
        // GET: Main/Delete/5
       
    }
}
