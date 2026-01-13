extends CanvasLayer

@onready var money_label = $MarginContainer/HBoxContainer/MoneyLabel
@onready var action_grid = $PanelContainer/MainLayout/ActionGrid

var current_money = 500

func _ready():
	# Connecter les boutons d'unités
	for button in action_grid.get_children():
		button.pressed.connect(_on_unit_button_pressed.bind(button.name))

func _on_unit_button_pressed(unit_name):
	if current_money >= 50: # Prix fictif pour l'exemple
		current_money -= 50
		update_ui()
		print("Production de : ", unit_name)
	else:
		print("Pas assez d'argent !")

func update_ui():
	money_label.text = "Or : " + str(current_money)
