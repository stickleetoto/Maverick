"""Extract the embedded raster figures the digitizers read, at their native resolution.

Usage: python extract_images.py 20080015840.pdf 20120013435.pdf
  TM-2008-214634 PDF p.21 -> tm2008_fig14_raw.png, PDF p.22 -> tm2008_fig15_raw.png
  TM-2012-215978 PDF pp.36-38 -> tm2012_fig27a/b, 28a/b, 29a/b_raw.png (a = pitch, b = rudder doublet)
"""
import sys
import pymupdf


def save_images(doc, page_number, names):
    page = doc[page_number - 1]
    images = page.get_images(full=True)
    if len(images) != len(names):
        raise SystemExit('PDF p.%d: %d images, expected %d' % (page_number, len(images), len(names)))
    for im, name in zip(images, names):
        pix = pymupdf.Pixmap(doc, im[0])
        if pix.n - pix.alpha >= 4 or pix.colorspace is None or pix.colorspace.n != 3:
            pix = pymupdf.Pixmap(pymupdf.csRGB, pix)
        pix.save(name)
        print(name, pix.width, pix.height, page.get_image_rects(im[0]))


tm2008 = pymupdf.open(sys.argv[1])
save_images(tm2008, 21, ['tm2008_fig14_raw.png'])
save_images(tm2008, 22, ['tm2008_fig15_raw.png'])

tm2012 = pymupdf.open(sys.argv[2])
for fig, page_number in ((27, 36), (28, 37), (29, 38)):
    save_images(tm2012, page_number, ['tm2012_fig%da_raw.png' % fig, 'tm2012_fig%db_raw.png' % fig])
