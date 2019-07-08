
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhpWin
{
    using Chromium;
    using Chromium.Event;
    using Chromium.WebBrowser;

    class CgiResource
    {
        internal static readonly string Scheme = "http";
        internal static readonly string Domain = "phpwin";

        internal static void GetResourceHandler(object sender, CfxGetResourceHandlerEventArgs e)
        {
            var uri = new Uri(e.Request.Url);
            if (uri.Scheme == Scheme && uri.Host == Domain)
            {
                try
                {
                    e.SetReturnValue(new CgiResourceHandler());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }


        internal static string GetFullUrl(string path)
        {
            return string.Format("{0}://{1}{2}", Scheme, Domain, path);
        }
    }


    class CgiResourceHandler : CfxResourceHandler
    {
        private readonly string rootdir = "htdocs";
        private readonly string execgi = "php72\\php-cgi.exe";

        private byte[] data;
        private string mimeType;
        private int httpStatus = 200;
        private string redirectUrl;

        private GCHandle gcHandle;

        private readonly string documentRoot;
        private readonly string execgiPath;
        private readonly string scriptExt = ".php";
        private readonly string scriptIndex = "index.php";
        private List<string[]> scriptHeaders = new List<string[]>();

        private int readResponseStreamOffset = 0;
        private int? buffStartPostition = null;
        private int? buffEndPostition = null;
        private bool isPartContent = false;


        internal CgiResourceHandler()
        {
            this.gcHandle = GCHandle.Alloc(this);
            this.documentRoot = System.IO.Path.Combine(Application.StartupPath, rootdir);
            this.execgiPath = System.IO.Path.Combine(Application.StartupPath, execgi);
            // Cookie
            this.CanSetCookie += (_, e) => { e.SetReturnValue(true); };
            this.CanGetCookie += (_, e) => { e.SetReturnValue(true); };
            // Events
            this.GetResponseHeaders += OnGetResponseHeaders;
            this.ProcessRequest += OnProcessRequest;
            this.ReadResponse += OnReadResponse;
        }

        private void OnProcessRequest(object sender, CfxProcessRequestEventArgs e)
        {
            var request = e.Request;

            var uri = new Uri(request.Url);

            var headers = request.GetHeaderMap().Select(x => new { Key = x[0], Value = x[1] }).ToList();

            var contentRange = headers.FirstOrDefault(x => x.Key.ToLower() == "range");

            if (contentRange != null)
            {
                var group = System.Text.RegularExpressions.Regex.Match(contentRange.Value, @"(?<start>\d+)-(?<end>\d*)").Groups;
                if (group != null)
                {
                    int startPos, endPos;
                    if (!string.IsNullOrEmpty(group["start"].Value) && int.TryParse(group["start"].Value, out startPos))
                    {
                        buffStartPostition = startPos;
                    }

                    if (!string.IsNullOrEmpty(group["end"].Value) && int.TryParse(group["end"].Value, out endPos))
                    {
                        buffEndPostition = endPos;
                    }
                }
                isPartContent = true;
            }

            readResponseStreamOffset = 0;

            if (buffStartPostition.HasValue)
            {
                readResponseStreamOffset = buffStartPostition.Value;
            }

            var filePath = Uri.UnescapeDataString(uri.AbsolutePath);
            if (string.IsNullOrEmpty(CgiResource.Domain))
            {
                filePath = string.Format("{0}{1}", uri.Authority, filePath);
            }
            filePath = filePath.Trim('/');

            var scriptName = filePath;
            var physicalPath = System.IO.Path.Combine(documentRoot, filePath);
            if (!System.IO.File.Exists(physicalPath))
            {
                scriptName = "";
                physicalPath = documentRoot;
                string[] splitPath = filePath.Split('/');
                foreach (string fileName in splitPath)
                {
                    var realPath = System.IO.Path.Combine(physicalPath, fileName);
                    if (!System.IO.Directory.Exists(realPath) && !System.IO.File.Exists(realPath))
                    {
                        break;
                    }
                    scriptName += "/" + fileName;
                    physicalPath = realPath;
                }
            }

            if (System.IO.Directory.Exists(physicalPath))
            {
                var realPath = System.IO.Path.Combine(physicalPath, scriptIndex);
                if (System.IO.File.Exists(realPath))
                {
                    scriptName += "/" + scriptIndex;
                    physicalPath = realPath;
                }
            }

            if (System.IO.File.Exists(physicalPath))
            {
                var fileExt = System.IO.Path.GetExtension(physicalPath);
                if (fileExt == scriptExt)
                {
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.Arguments = physicalPath;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.FileName = execgiPath;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.WorkingDirectory = documentRoot;

                   process.StartInfo.EnvironmentVariables.Add("SERVER_SOFTWARE", "PhpWin");
                   process.StartInfo.EnvironmentVariables.Add("SERVER_NAME", uri.Host);
                   process.StartInfo.EnvironmentVariables.Add("SERVER_PORT", uri.Port.ToString());
                   process.StartInfo.EnvironmentVariables.Add("SERVER_PROTOCOL", "HTTP/1.1");
                   process.StartInfo.EnvironmentVariables.Add("HTTP_HOST", uri.Host);
                   process.StartInfo.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
                   process.StartInfo.EnvironmentVariables.Add("REQUEST_METHOD", request.Method);
                    if (uri.Query.Length > 1)
                    {
                        process.StartInfo.EnvironmentVariables.Add("QUERY_STRING", uri.Query.Substring(1));
                    }
                    process.StartInfo.EnvironmentVariables.Add("REQUEST_URI", string.Format("/{0}{1}", filePath, uri.Query));
                    if (filePath.Length > scriptName.Length)
                    {
                        process.StartInfo.EnvironmentVariables.Add("PATH_INFO", filePath.Substring(scriptName.Length - 1));
                    }
                    process.StartInfo.EnvironmentVariables.Add("SCRIPT_NAME", scriptName);
                    process.StartInfo.EnvironmentVariables.Add("DOCUMENT_URI", "/" + filePath);
                    process.StartInfo.EnvironmentVariables.Add("DOCUMENT_ROOT", documentRoot);
                    process.StartInfo.EnvironmentVariables.Add("SCRIPT_FILENAME", physicalPath);
                    process.StartInfo.EnvironmentVariables.Add("REDIRECT_STATUS", "200");

                    foreach (var item in headers)
                    {
                        if (item.Key == "PROXY")
                        {
                            continue;
                        }
                        if (item.Key.ToUpper() == "CONTENT-TYPE")
                        {
                            process.StartInfo.EnvironmentVariables.Add("CONTENT_TYPE", item.Value);
                        }
                        else
                        {
                            process.StartInfo.EnvironmentVariables.Add("HTTP_" + item.Key.Replace("-", "_").ToUpper(), item.Value);
                        }
                    }

                    ulong contentLength = 0;
                    if (request.PostData != null)
                    {
                        foreach (var element in request.PostData.Elements)
                        {
                            if (element.Type == CfxPostdataElementType.Bytes)
                            {
                                contentLength += element.BytesCount;
                            }
                            else if (element.Type == CfxPostdataElementType.File)
                            {
                                var fileInfo = new System.IO.FileInfo(element.File);
                                contentLength += Convert.ToUInt64(fileInfo.Length);
                            }
                        }
                    }

                    process.StartInfo.EnvironmentVariables.Add("CONTENT_LENGTH", contentLength.ToString());

                    if (process.Start())
                    {
                        if (request.PostData != null && contentLength > 0)
                        {
                            foreach (var element in request.PostData.Elements)
                            {
                                if (element.Type == CfxPostdataElementType.Bytes)
                                {
                                    var buffer = new byte[element.BytesCount];
                                    GCHandle hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                                    IntPtr pBuffer = hBuffer.AddrOfPinnedObject();

                                    var count = element.GetBytes(element.BytesCount, pBuffer);
                                    process.StandardInput.Write(Encoding.ASCII.GetChars(buffer, 0, Convert.ToInt32(count)));

                                    if (hBuffer.IsAllocated)
                                        hBuffer.Free();
                                }
                                else if (element.Type == CfxPostdataElementType.File)
                                {
                                    try
                                    {
                                        var buffer = System.IO.File.ReadAllBytes(element.File);
                                        process.StandardInput.BaseStream.Write(buffer, 0, Convert.ToInt32(buffer.Length));
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(ex.Message);
                                        e.Callback.Dispose();
                                        e.SetReturnValue(false);
                                        return;
                                    }
                                }
                            }

                        }
                    }

                    using (var output = new System.IO.MemoryStream())
                    {
                        int read;
                        byte[] buffer = new byte[16 * 1024];
                        var stream = process.StandardOutput.BaseStream;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                        }
                        output.Seek(0, System.IO.SeekOrigin.Begin);

                        var offset = 0;
                        var reader = new System.IO.StreamReader(output);
                        while (!reader.EndOfStream)
                        {
                            var readline = reader.ReadLine();
                            offset += readline.Length + 2;
                            if (readline.Equals(""))
                            {
                                break;
                            }
                            var header = readline.Split(':');
                            if (header.Length < 2)
                            {
                                break;
                            }
                            header[1] = header[1].Trim();
                            if (header[0].ToUpper() == "CONTENT-TYPE")
                            {
                                mimeType = header[1].Split(';')[0].Trim();
                            }
                            else if (header[0].ToUpper() == "STATUS")
                            {
                                httpStatus = int.Parse(header[1].Split(' ')[0]);
                            }
                            else if (header[0].ToUpper() == "LOCATION")
                            {
                                if (header[1].StartsWith("/"))
                                {
                                    redirectUrl = CgiResource.GetFullUrl(header[1]);
                                }
                                else
                                {
                                    redirectUrl = header[1];
                                }
                            }
                            scriptHeaders.Add(header);
                        }

                        var count = output.Length - offset;
                        data = new byte[count];
                        output.Seek(offset, System.IO.SeekOrigin.Begin);
                        output.Read(data, 0, Convert.ToInt32(count));
                    }

                }
                else
                {
                    data = System.IO.File.ReadAllBytes(physicalPath);
                    mimeType = CfxRuntime.GetMimeType(fileExt.TrimStart('.'));
                }

                e.Callback.Continue();
                e.SetReturnValue(true);
            }
            else
            {
                e.Callback.Continue();
                e.SetReturnValue(false);
            }

        }

        private void OnGetResponseHeaders(object sender, CfxGetResponseHeadersEventArgs e)
        {

            if (data == null)
            {
                e.Response.Status = 404;
            }
            else
            {
                var length = data.Length;
                e.ResponseLength = data.Length;
                e.Response.MimeType = mimeType;
                e.Response.Status = httpStatus;
                e.RedirectUrl = redirectUrl;

                var headers = e.Response.GetHeaderMap().Union(scriptHeaders).ToList<string[]>();

                if (isPartContent)
                {
                    headers.Add(new string[2] { "Accept-Ranges", "bytes" });
                    var startPos = 0;
                    var endPos = length - 1;

                    if (buffStartPostition.HasValue && buffEndPostition.HasValue)
                    {
                        startPos = buffStartPostition.Value;
                        endPos = buffEndPostition.Value;
                    }
                    else if (!buffEndPostition.HasValue && buffStartPostition.HasValue)
                    {
                        startPos = buffStartPostition.Value;
                    }

                    headers.Add(new string[2] { "Content-Range", "bytes {startPos}-{endPos}/{webResource.data.Length}" });
                    headers.Add(new string[2] { "Content-Length", "{endPos - startPos + 1}" });

                    e.ResponseLength = (endPos - startPos + 1);

                    e.Response.Status = 206;
                }
                e.Response.SetHeaderMap(headers);
            }
        }

        private void OnReadResponse(object sender, CfxReadResponseEventArgs e)
        {
            int bytesToCopy = data.Length - readResponseStreamOffset;

            if (bytesToCopy > e.BytesToRead)
                bytesToCopy = e.BytesToRead;

            Marshal.Copy(data, readResponseStreamOffset, e.DataOut, bytesToCopy);

            e.BytesRead = bytesToCopy;

            readResponseStreamOffset += bytesToCopy;

            e.SetReturnValue(true);

            if (readResponseStreamOffset == data.Length)
            {
                gcHandle.Free();
            }
        }
    }
}
