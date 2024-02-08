//using Microsoft.AspNetCore.Mvc;

//namespace ReleaseShip.Controllers
//{

//    [ApiController]
//    [Route("/oci/")]
//    public class ContainerDistributionController : ControllerBase
//    {
//        [HttpGet("blobs/{digest}")]
//        public async Task<IActionResult> GetBlob(string digest)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpGet("manifests/{reference}")]
//        public async Task<IActionResult> GetManifest(string reference)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpGet("tags/list")]
//        public async Task<IActionResult> GetTagsList()
//        {
//            throw new NotImplementedException();
//        }

//        [HttpPut("blobs/uploads/{uuid}")]
//        public async Task<IActionResult> StartBlobUpload(string uuid)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpPatch("blobs/uploads/{uuid}")]
//        public async Task<IActionResult> UploadBlobChunk(string uuid)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpDelete("blobs/{digest}")]
//        public async Task<IActionResult> DeleteBlob(string digest)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpGet("blobs/{digest}/exists")]
//        public async Task<IActionResult> CheckBlobExists(string digest)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpGet("repositories/{name}/tags/{tag}")]
//        public async Task<IActionResult> GetTag(string name, string tag)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpDelete("repositories/{name}/tags/{tag}")]
//        public async Task<IActionResult> DeleteTag(string name, string tag)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpHead("repositories/{name}/tags/{tag}")]
//        public async Task<IActionResult> CheckTagExists(string name, string tag)
//        {
//            throw new NotImplementedException();
//        }

//        [HttpGet("repositories/{name}/tags/list")]
//        public async Task<IActionResult> GetRepositoryTagsList(string name)
//        {
//            throw new NotImplementedException();
//        }
//    }

//}
