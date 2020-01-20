using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RFIT.Models;

using System.IO;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json.Linq;

namespace RFIT.Controllers
{
    // All request send a ReponseData as result
    public class ResponseData
    {
        // Req type (add, edit, delete)
        public String reqType { get; set;  }
        // Material object to work with
        public Material material { get; set; }
        // Messages (error or success) to send to the View
        public List<String> messages { get; set; }
        // Error boolean
        public bool isError { get; set; }
    }
    public class HomeController : Controller
    {
        private String _imageFolderPath = "uploads";
        private int _imageMaxBytesLength = 10000;
        private readonly MaterialDbContext _db;
        private IWebHostEnvironment _environment;
        private dynamic _translate;
        public HomeController(MaterialDbContext db, IWebHostEnvironment environment)
        {
            _environment = environment;
            _db = db;
            _translate = JValue.Parse(System.IO.File.ReadAllText("BackTranslate.json", Encoding.GetEncoding("iso-8859-1")));
        }

        // This function convert timestamp to date type
        public DateTime TimestampToDate(long timestamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(timestamp).ToLocalTime();
            return dtDateTime;
        }

        // This function return an empty list if no error founded in model fields, else return all error messages
        public List<String> VerifyMaterial(Material material)
        {
            String lang = GetLanguage();
            material = material == null ? new Material() : material;
            var context = new ValidationContext(material, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();
            List<String> messages = new List<String>();

            // Check if SerialNumber is unique
            var checkIfUnique = _db.Materials.FirstOrDefault(item => item.SerialNumber == material.SerialNumber && item.Id != material.Id);
            if (checkIfUnique != null)
                messages.Add((String)_translate.uniqueSerialNumber[lang]); // Error: Serial number must be unique

            // Check constraint attribute of material fields
            if (!Validator.TryValidateObject(material, context, results, true))
            {
                for (int i = 0; i < results.Count; ++i)
                    messages.Add(results[i].ErrorMessage);
            }
            return messages;
        }

        // Default action
        public ActionResult Index()
        {
            ViewBag.Materials = _db.Materials.ToList();
            ViewBag.TimestampToDate = new Func<long, DateTime>(TimestampToDate);
            ViewBag.lang = GetLanguage();
            ViewBag.translate = _translate;
            return View();
        }

        // REQUEST Add Material
        [HttpPost]
        public ResponseData AddMaterial([FromBody] Material material)
        {
            String lang = GetLanguage();
            List<String> verifyResults = VerifyMaterial(material);
            bool isError = false;


            if (material.InspectionDate <= (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds)
                verifyResults.Add((String)_translate.inspectionDateGreater[lang]);

            // No error, add material to database
            if (verifyResults.Count == 0)
            {
                material.ImagePath = "/images/default_image.png";
                var entityRef = _db.Materials.Add(material);
                _db.SaveChanges();
                material = entityRef.Entity;
                verifyResults = new List<String>() { (String)_translate.materialAddSuccess[lang] }; // Sucess: Material added
            }
            // // Error with VerifiyMaterial(), abort 
            else
                isError = true;

            return new ResponseData()
            {
                reqType = "add",
                material = material,
                messages = verifyResults,
                isError = isError
            };
        }

        // REQUEST Add Image (Request after EditMaterial or AddMaterial with success)
        [HttpPost]
        public ResponseData AddImage()
        {
            String lang = GetLanguage();
            Material entity = null;
            var file = Request.Form.Files[0];
            int id = int.Parse(Request.Form.Where(x => x.Key == "id").FirstOrDefault().Value);

            List<String> verifyResults = new List<String>();
            bool isError = false;

            if (file != null)
                {
                String uploads = Path.Combine(_environment.WebRootPath, _imageFolderPath);
                if (file.Length < _imageMaxBytesLength * 1000)
                {
                    String fileExtension = Path.GetExtension(file.FileName).ToLower();
                    if (fileExtension == ".png" || fileExtension == ".jpg")
                    {
                        String fileName = id.ToString() + fileExtension;
                        String filePath = Path.Combine(uploads, fileName);
                        entity = _db.Materials.FirstOrDefault(item => item.Id == id);
                        if (entity != null)
                        {
                            // Material found, let's add image
                            if (entity.ImagePath != null)
                            {
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                System.IO.File.Delete(filePath);
                            }
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                                file.CopyTo(fileStream);
                            entity.ImagePath = "/" + _imageFolderPath + "/" + fileName;
                            _db.Materials.Update(entity);
                            _db.SaveChanges();
                        }
                        else
                        {
                            // Error, material not found
                            isError = true;
                            verifyResults.Add((String)_translate.imageMaterialNotFound[lang]); // Error : Material not found then no image was uploaded
                        }
                    }
                    else
                    {
                        // Error, bad extension
                        isError = true;
                        verifyResults.Add((String)_translate.imageBadExtension[lang]); // Error : Bad image extension
                    }
                }
                else
                {
                    // Error, file size too big
                    isError = true;
                    verifyResults.Add((String)_translate.imageTooBig[lang] + _imageMaxBytesLength + " Mo"); // Error : File too big
                }
            }
            return new ResponseData()
            {
                reqType = "addImage",
                material = entity,
                messages = verifyResults,
                isError = isError
            };
        }

        // REQUEST Edit Material
        [HttpPost]
        public ResponseData EditMaterial([FromBody] Material material)
        {
            String lang = GetLanguage();
            List<String> verifyResults = VerifyMaterial(material);
            bool isError = false;

            // No error, edit material in database
            if (verifyResults.Count == 0)
            {
                var entity = _db.Materials.FirstOrDefault(item => item.Id == material.Id);
                if (entity != null)
                {
                    // Material found, let's edit it
                    entity.Name = material.Name;
                    entity.SerialNumber = material.SerialNumber;
                    entity.InspectionDate = material.InspectionDate;
                    _db.Materials.Update(entity);
                    _db.SaveChanges();
                    verifyResults.Add((String)_translate.materialEditSuccess[lang]); // Success : Material was edited
                }
                else
                {
                    // Error, material not found
                    isError = true;
                    verifyResults.Add((String)_translate.materialNotFound[lang]); // Error : Material not found
                }
            }
            // Error with VerifiyMaterial(), abort
            else
                isError = true;

            return new ResponseData()
            {
                reqType = "edit",
                material = material,
                messages = verifyResults,
                isError = isError
            };
        }

        // REQUEST Delete Material
        [HttpPost]
        public ResponseData DeleteMaterial([FromBody] Material material)
        {
            String lang = GetLanguage();
            var entity = _db.Materials.FirstOrDefault(item => item.Id == material.Id);
            // If image exist, try delete it
            if (entity != null && entity.ImagePath != null)
            {
                String filePath = Path.Combine(
                    Path.Combine(_environment.WebRootPath, _imageFolderPath),
                    Path.GetFileName(entity.ImagePath)
                );
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.IO.File.Delete(filePath);
            }
            // Remove material from database
            try
            {
                _db.Remove(_db.Materials.Single(a => a.Id == material.Id));
                _db.SaveChanges();
            }
            catch { };
            return new ResponseData()
            {
                reqType = "delete",
                material = material,
                messages = new List<String>() { (String)_translate.materialDeleteSuccess[lang] }, // Success : Material was deleted
                isError = false
            };
        }

        // Check if language exist
        public bool LanguageExist(String lang)
        {
            List<String> languages = new List<String>() { "fr" , "en"};
            for (int i = 0; i < languages.Count; ++i)
            {
                if (languages[i] == lang)
                    return true;
            }
            return false;
        }

        // Return language keyword (fr, en)
        public string GetLanguage()
        {
            String lang;
            try { lang = AppHttpContext.Current.Session.GetString("lang"); }
            catch { lang = "fr"; }
            return lang == null ? "fr" : lang;
        }

        // Set language in session
        [HttpPost]
        public ResponseData SetLanguage([FromBody] String lang)
        {
            String curLang = GetLanguage();
            bool isError = false;
            List<String> messages = new List<string>();

            if (LanguageExist(lang))
                AppHttpContext.Current.Session.SetString("lang", lang);
            else
            {
                isError = true;
                messages.Add((String)_translate.languageNotExist[curLang] + " -> " + lang); // Error : Language not exist
            }

            return new ResponseData()
            {
                reqType = "setLanguage",
                messages = messages,
                isError = isError
            };
        }
    }
}
