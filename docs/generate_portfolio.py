r"""Generate the portfolio PNG, animated GIF and PDF with Pillow.

From the repository root, run: python docs\generate_portfolio.py
Requires Pillow and the Windows Segoe UI fonts.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FONTS = Path(r"C:\Windows\Fonts")
W, H = 1600, 1000
BG = "#0b1120"
PANEL = "#16243d"
BORDER = "#34507a"
TEXT = "#eef4ff"
MUTED = "#bdcde6"
ACCENT = "#72e0df"
GOLD = "#ffd479"


def font(size, bold=False):
	return ImageFont.truetype(str(FONTS / ("segoeuib.ttf" if bold else "segoeui.ttf")), size)


def wrap(draw, text, face, width):
	lines = []
	line = ""
	for word in text.split():
		candidate = f"{line} {word}".strip()
		if line and draw.textlength(candidate, font=face) > width:
			lines.append(line)
			line = word
		else:
			line = candidate
	if line:
		lines.append(line)
	return lines


def paragraph(draw, text, x, y, width, size=27, color=MUTED, spacing=11):
	face = font(size)
	for line in wrap(draw, text, face, width):
		draw.text((x, y), line, font=face, fill=color)
		y += size + spacing
	return y


def base(section, number):
	image = Image.new("RGB", (W, H), BG)
	draw = ImageDraw.Draw(image)
	draw.rectangle((0, 0, W, 9), fill=ACCENT)
	draw.text((80, 52), "SOFTWARE ENGINEERING  /  PORTFOLIO", font=font(22, True), fill=ACCENT)
	draw.text((80, 105), section, font=font(58, True), fill=TEXT)
	draw.line((80, 203, W - 80, 203), fill=BORDER, width=2)
	draw.text((80, 949), "PAUANESH KUMAR  |  GITHUB.COM/PAUANESH-KUMAR", font=font(18, True), fill=MUTED)
	draw.text((1440, 949), f"{number} / 3", font=font(18, True), fill=MUTED)
	return image, draw


def card(draw, box, title, body, accent=ACCENT):
	x, y, right, bottom = box
	draw.rounded_rectangle(box, radius=20, fill=PANEL, outline=BORDER, width=2)
	draw.text((x + 28, y + 22), title, font=font(29, True), fill=accent)
	end = paragraph(draw, body, x + 28, y + 77, right - x - 56, size=24, spacing=10)
	if end > bottom - 15:
		raise ValueError(f"Card content exceeds its bounds: {title}")


def cover():
	image, d = base("E-Commerce Microservices", 1)
	paragraph(d, "A .NET backend exploring service boundaries, event-driven commerce, resilient integrations and data ownership.", 80, 232, 1360, size=31)
	d.text((80, 350), ".NET 10   /   KAFKA   /   POSTGRESQL   /   MONGODB   /   REDIS", font=font(23, True), fill=GOLD)
	card(d, (80, 425, 780, 600), "Commerce services", "User, product, cart, order and payment APIs divide responsibilities across independent projects.")
	card(d, (820, 425, 1520, 600), "Event-driven integration", "Kafka consumers and transactional outbox code connect the order and payment workflows.")
	card(d, (80, 625, 780, 800), "Data & delivery", "PostgreSQL, MongoDB and Redis; Docker Compose, YARP and observability tooling.")
	card(d, (820, 625, 1520, 800), "Recovery design", "Transactional inventory and payment reconciliation implemented and tested in isolation.")
	d.text((80, 849), "ISOLATED FLOWS TESTED  |  NOT PRODUCTION-READY", font=font(24, True), fill=GOLD)
	return image


def architecture():
	image, d = base("Architecture & workflows", 2)
	paragraph(d, "A layered, service-oriented codebase with synchronous checkout and asynchronous event handling.", 80, 235, 1380, size=30)
	labels = [("CLIENT", "HTTP client"), ("COMMERCE", "Cart / Order"), ("MESSAGING", "Kafka / Outbox"), ("CONSUMERS", "Payment / Analytics")]
	for i, (heading, label) in enumerate(labels):
		x = 80 + i * 365
		d.rounded_rectangle((x, 355, x + 320, 505), radius=20, fill=PANEL, outline=ACCENT, width=2)
		d.text((x + 22, 380), heading, font=font(19, True), fill=ACCENT)
		d.text((x + 22, 424), label, font=font(25, True), fill=TEXT)
		if i < len(labels) - 1:
			d.line((x + 325, 430, x + 357, 430), fill=MUTED, width=4)
	card(d, (80, 570, 780, 760), "Inventory consistency", "MongoDB transactions combine reservation, inbox and outbox changes; delivery is at least once.")
	card(d, (820, 570, 1520, 760), "Payment recovery", "Durable payment intent, signed webhooks and provider reconciliation handle uncertain outcomes.")
	d.text((80, 835), "Replica-set migration and real payment sandbox validation remain.", font=font(23, True), fill=GOLD)
	return image


def delivery():
	image, d = base("Delivery & next steps", 3)
	paragraph(d, "A practical engineering portfolio focused on system design and implementation evidence.", 80, 235, 1360, size=31)
	card(d, (80, 350, 780, 560), "Current implementation", "Layered ASP.NET Core services, YARP routing, Kafka consumers, outbox publishers and a Razorpay integration.")
	card(d, (820, 350, 1520, 560), "Infrastructure", "Compose describes the services and supporting databases, cache, messaging and monitoring tools.")
	card(d, (80, 595, 780, 805), "Verification status", "xUnit and isolated checkout, compensation, retry and duplicate-delivery tests. Payment provider is a stub.", GOLD)
	card(d, (820, 595, 1520, 805), "Next engineering work", "Migrate Product MongoDB, deploy updated services, remediate advisories and test a real sandbox.", GOLD)
	d.text((80, 849), "PROJECT: GITHUB.COM/PAUANESH-KUMAR/ECOMMERCEMICROSERVICESAPP", font=font(22, True), fill=ACCENT)
	return image


def main():
	pages = [cover(), architecture(), delivery()]
	pages[0].save(HERE / "freelancer-portfolio.png", optimize=True)
	pages[0].save(HERE / "freelancer-portfolio.gif", save_all=True, append_images=pages[1:], duration=3500, loop=0, optimize=True)
	pages[0].save(HERE / "freelancer-portfolio.pdf", save_all=True, append_images=pages[1:], resolution=120.0)
	print("Created freelancer-portfolio.png, freelancer-portfolio.gif and freelancer-portfolio.pdf")


if __name__ == "__main__":
	main()
