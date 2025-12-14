namespace BotGenerator.Core.Services;

/// <summary>
/// Provides variation in pre-determined bot responses for a more natural conversation.
/// Each method returns a random variation of a specific response type.
/// </summary>
public static class ResponseVariations
{
    private static readonly Random _random = new();

    /// <summary>
    /// Large group (>10 people) contact card response.
    /// </summary>
    public static string LargeGroupVCard() => Pick(new[]
    {
        "Te he enviado la tarjeta de contacto de nuestro equipo de reservas. Ellos te ayudarán con la reserva para tu grupo. ¡Gracias!",
        "Ahí tienes el contacto de nuestro equipo de reservas. Te atenderán personalmente para organizar tu grupo. ¡Un saludo!",
        "Te paso el contacto directo con nuestro equipo. Ellos se encargarán de tu reserva para el grupo. ¡Gracias por elegir Villa Carmen!",
        "Ya tienes la tarjeta de contacto. Nuestro equipo te ayudará con todos los detalles de la reserva. ¡Hasta pronto!",
        "Listo, te he enviado el contacto. Para grupos grandes te atenderán ellos directamente. ¡Gracias!",
        "Ahí va el contacto de reservas. Para grupos numerosos, ellos te darán mejor atención. ¡Gracias por contar con nosotros!",
        "Te envío la tarjeta del equipo de reservas. Para un grupo como el vuestro, os atenderán de forma personalizada.",
        "Aquí tienes el contacto. Nuestro equipo especializado en grupos te echará una mano. ¡Un saludo!",
        "Te he pasado la tarjeta. Ellos os organizarán todo para que la experiencia sea perfecta. ¡Gracias!",
        "Perfecto, ahí tienes el contacto directo. Para grupos os atienden ellos personalmente. ¡Nos vemos pronto!"
    });

    /// <summary>
    /// Special request (cakes, celebrations) contact card response.
    /// </summary>
    public static string SpecialRequestVCard() => Pick(new[]
    {
        "Te he enviado la tarjeta de contacto. Nuestro equipo te ayudará con tu solicitud especial. ¡Gracias!",
        "Ahí tienes el contacto. Para peticiones especiales, nuestro equipo te atenderá directamente. ¡Un saludo!",
        "Ya tienes la tarjeta. Ellos te ayudarán con todo lo que necesitas para tu celebración. ¡Gracias por confiar en nosotros!",
        "Te paso el contacto de nuestro equipo. Te ayudarán a preparar algo especial. ¡Hasta pronto!",
        "Listo, te he enviado el contacto. Para solicitudes como esta, te atenderán personalmente. ¡Gracias!",
        "Aquí tienes la tarjeta del equipo. Para ocasiones especiales, ellos os prepararán algo único.",
        "Te envío el contacto de nuestro equipo de eventos. Ellos harán que tu celebración sea perfecta.",
        "Ahí va la tarjeta. Nuestro equipo te ayudará a organizar todos los detalles especiales. ¡Un saludo!",
        "Perfecto, te paso el contacto. Para tartas y celebraciones, ellos os atenderán de maravilla.",
        "Ya tienes el contacto directo. Ellos se encargarán de que tu evento sea memorable. ¡Gracias!"
    });

    /// <summary>
    /// Asking for booking date.
    /// </summary>
    public static string AskDate() => Pick(new[]
    {
        "¿Para qué día os gustaría reservar?",
        "¿Qué día os viene bien?",
        "¿Para qué fecha sería?",
        "¿Qué día queréis venir?",
        "¿Para cuándo sería la reserva?",
        "¿Y para qué día queréis la mesa?",
        "¿Qué día preferís para venir?",
        "Dime, ¿para qué día sería?",
        "¿Qué día tenéis pensado?",
        "¿Para cuándo os reservo mesa?"
    });

    /// <summary>
    /// Asking for booking time.
    /// </summary>
    public static string AskTime() => Pick(new[]
    {
        "¿A qué hora os gustaría reservar?",
        "¿A qué hora os viene bien?",
        "¿A qué hora queréis venir?",
        "¿Qué hora os vendría mejor?",
        "¿Para qué hora sería?",
        "¿Y a qué hora os espero?",
        "¿Qué hora preferís?",
        "Dime, ¿a qué hora sería?",
        "¿A qué hora tenéis pensado venir?",
        "¿Para qué hora os va bien?"
    });

    /// <summary>
    /// Asking about highchairs.
    /// </summary>
    public static string AskTronas() => Pick(new[]
    {
        "Antes de confirmarla, ¿necesitáis *tronas*?",
        "¿Vais a necesitar *tronas* para los peques?",
        "¿Necesitáis alguna *trona* para niños?",
        "¿Necesitáis *tronas*?",
        "¿Os preparo alguna *trona*?",
        "¿Venís con niños pequeños? ¿Necesitáis *tronas*?",
        "¿Traéis peques? ¿Os pongo *tronas*?",
        "Una cosita, ¿necesitáis *tronas* para los niños?",
        "¿Queréis que os prepare alguna *trona*?",
        "¿Lleváis niños que necesiten *trona*?"
    });

    /// <summary>
    /// Asking about strollers.
    /// </summary>
    public static string AskCarritos() => Pick(new[]
    {
        "¿Vais a traer *carrito de bebé*?",
        "¿Traéis *carrito de bebé*?",
        "¿Venís con *carrito de bebé*?",
        "¿Necesitáis espacio para *carrito de bebé*?",
        "¿Lleváis *carrito*?",
        "¿Os reservo sitio para *carrito de bebé*?",
        "¿Venís con algún *carrito*?",
        "¿Traéis *carrito* para los peques?",
        "¿Necesitáis aparcar algún *carrito*?",
        "¿Hay que dejar espacio para *carrito de bebé*?"
    });

    /// <summary>
    /// Asking about rice.
    /// </summary>
    public static string AskRice() => Pick(new[]
    {
        "Una cosa más: ¿queréis *arroz*? (sí/no)",
        "¿Os apetece *arroz*?",
        "¿Queréis añadir *arroz* a la reserva?",
        "¿Os preparo *arroz*?",
        "¿Vais a querer *arroz*?",
        "Por cierto, ¿os apetece pedir *arroz*?",
        "¿Habéis pensado si queréis *arroz*?",
        "¿Os gustaría añadir *arroz* a vuestra reserva?",
        "Y una cosita más: ¿queréis *arroz*?",
        "¿Queréis que os reserve *arroz*?"
    });

    /// <summary>
    /// Asking for rice portions.
    /// </summary>
    public static string AskRicePortions() => Pick(new[]
    {
        "Perfecto. ¿Cuántas *raciones* de arroz queréis? (mínimo 2)",
        "¿Cuántas *raciones* os preparo? (mínimo 2)",
        "¿Cuántas *raciones* queréis? Recordad que el mínimo son 2.",
        "Genial. ¿Cuántas *raciones* necesitáis? (mínimo 2)",
        "¿Y cuántas *raciones* serían? (mínimo 2)",
        "Vale, ¿cuántas *raciones* de arroz? (mínimo 2)",
        "¿Cuántas *raciones* os pongo? El mínimo son 2.",
        "Estupendo. ¿Cuántas *raciones*? (mínimo 2)",
        "¿Para cuántas *raciones* de arroz? (mínimo 2)",
        "Muy bien. ¿Cuántas *raciones* queréis? (mínimo 2)"
    });

    /// <summary>
    /// Asking how many highchairs.
    /// </summary>
    public static string AskTronasCount() => Pick(new[]
    {
        "Perfecto. ¿Cuántas tronas necesitáis?",
        "Vale. ¿Cuántas tronas os preparo?",
        "¿Cuántas tronas necesitáis?",
        "Genial. ¿Cuántas serían?",
        "De acuerdo. ¿Cuántas tronas?",
        "Estupendo. ¿Cuántas tronas os pongo?",
        "Muy bien. ¿Cuántas tronas queréis?",
        "¿Y cuántas tronas serían?",
        "Entendido. ¿Cuántas tronas necesitáis?",
        "¿Cuántas tronas os reservo?"
    });

    /// <summary>
    /// Asking how many strollers.
    /// </summary>
    public static string AskCarritosCount() => Pick(new[]
    {
        "Vale. ¿Cuántos carritos vais a traer?",
        "¿Cuántos carritos traéis?",
        "De acuerdo. ¿Cuántos carritos serían?",
        "¿Cuántos carritos necesitáis aparcar?",
        "Perfecto. ¿Cuántos carritos?",
        "Entendido. ¿Cuántos carritos lleváis?",
        "¿Y cuántos carritos traéis?",
        "Muy bien. ¿Cuántos carritos serían?",
        "¿Cuántos carritos os reservo sitio?",
        "Genial. ¿Cuántos carritos venís?"
    });

    /// <summary>
    /// Greeting response.
    /// </summary>
    public static string Greeting(string name) => Pick(new[]
    {
        $"¡Hola {name}! ¿Quieres hacer una reserva?",
        $"¡Hola {name}! ¿En qué puedo ayudarte?",
        $"¡Buenas {name}! ¿Te ayudo con una reserva?",
        $"¡Hola {name}! ¿Qué tal? ¿Quieres reservar mesa?",
        $"¡Hola {name}! Encantado de saludarte. ¿En qué te ayudo?",
        $"¡Buenas {name}! ¿Necesitas ayuda con una reserva?",
        $"¡Hola {name}! ¿Qué te trae por aquí?",
        $"¡Hola {name}! Bienvenido a Villa Carmen. ¿En qué puedo ayudarte?",
        $"¡Qué tal {name}! ¿Te echo una mano con la reserva?",
        $"¡Hola {name}! Un placer saludarte. ¿Quieres reservar?"
    });

    /// <summary>
    /// Maximum tronas/carritos warning.
    /// </summary>
    public static string MaxTronas() => Pick(new[]
    {
        "Podemos preparar como máximo *3* tronas. ¿Cuántas necesitáis?",
        "Solo disponemos de *3* tronas como máximo. ¿Cuántas os preparo?",
        "Tenemos un máximo de *3* tronas. ¿Cuántas necesitáis?",
        "El máximo son *3* tronas. ¿Cuántas queréis?",
        "Disponemos de hasta *3* tronas. ¿Cuántas os reservo?",
        "Lo siento, el límite son *3* tronas. ¿Cuántas os pongo?",
        "Como máximo podemos preparar *3* tronas. ¿Cuántas serían?",
        "Perdona, solo tenemos *3* tronas disponibles. ¿Cuántas queréis?",
        "Nuestro máximo es *3* tronas. ¿Cuántas necesitáis?",
        "Tenemos tope de *3* tronas. ¿Cuántas os preparo?"
    });

    /// <summary>
    /// Maximum carritos warning.
    /// </summary>
    public static string MaxCarritos() => Pick(new[]
    {
        "Podemos gestionar como máximo *3* carritos. ¿Cuántos vais a traer?",
        "Solo podemos acomodar *3* carritos. ¿Cuántos traéis?",
        "Tenemos espacio para un máximo de *3* carritos. ¿Cuántos serían?",
        "El máximo son *3* carritos. ¿Cuántos necesitáis?",
        "Disponemos de espacio para *3* carritos como máximo. ¿Cuántos?",
        "Lo siento, el límite son *3* carritos. ¿Cuántos traéis?",
        "Como máximo tenemos sitio para *3* carritos. ¿Cuántos serían?",
        "Perdona, solo podemos aparcar *3* carritos. ¿Cuántos venís?",
        "Nuestro máximo es *3* carritos. ¿Cuántos lleváis?",
        "Tenemos tope de *3* carritos. ¿Cuántos necesitáis?"
    });

    /// <summary>
    /// Minimum rice portions warning.
    /// </summary>
    public static string MinRicePortions() => Pick(new[]
    {
        "Para los arroces el mínimo es *2 raciones*. ¿Cuántas queréis?",
        "El mínimo de arroz son *2 raciones*. ¿Cuántas os preparo?",
        "Servimos el arroz en mínimo *2 raciones*. ¿Cuántas necesitáis?",
        "Nuestros arroces van de *2 raciones* en adelante. ¿Cuántas queréis?",
        "El arroz sale en mínimo *2 raciones*. ¿Cuántas os pongo?",
        "Lo siento, el arroz se sirve mínimo *2 raciones*. ¿Cuántas serían?",
        "Perdona, el mínimo son *2 raciones* de arroz. ¿Cuántas queréis?",
        "El arroz tiene un mínimo de *2 raciones*. ¿Cuántas os preparo?",
        "Como mínimo son *2 raciones* de arroz. ¿Cuántas necesitáis?",
        "Preparamos el arroz desde *2 raciones*. ¿Cuántas os pongo?"
    });

    // ========== MODIFICATION FLOW RESPONSES ==========

    /// <summary>
    /// No future bookings found for this phone number.
    /// </summary>
    public static string ModificationNoBookingsFound() => Pick(new[]
    {
        "No he encontrado ninguna reserva futura con este número de teléfono. ¿Quieres hacer una nueva reserva?",
        "No tengo reservas pendientes asociadas a este teléfono. ¿Te ayudo a crear una reserva nueva?",
        "No encuentro reservas futuras con tu número. ¿Necesitas hacer una reserva?",
        "Vaya, no hay ninguna reserva próxima con este teléfono. ¿Quieres reservar mesa?",
        "No aparece ninguna reserva futura tuya. ¿Hacemos una reserva nueva?",
        "No tengo constancia de reservas pendientes con este número. ¿Te ayudo a reservar?",
        "No encuentro ninguna reserva activa con tu teléfono. ¿Quieres que te reserve mesa?",
        "Mmm, no veo reservas futuras asociadas a tu número. ¿Hacemos una nueva?",
        "No hay reservas pendientes con este teléfono. ¿Reservamos mesa?",
        "No tengo reservas registradas con este número para próximas fechas. ¿Te ayudo con una nueva?"
    });

    /// <summary>
    /// Asking which booking to modify when multiple exist.
    /// </summary>
    public static string ModificationSelectBooking() => Pick(new[]
    {
        "¿Cuál de estas reservas quieres modificar?",
        "Dime cuál de las reservas te gustaría cambiar.",
        "¿Qué reserva necesitas modificar?",
        "¿Cuál de ellas quieres modificar?",
        "Indícame cuál reserva quieres cambiar.",
        "¿Sobre cuál de las reservas hacemos el cambio?",
        "¿Qué reserva te gustaría modificar?",
        "Dime, ¿cuál de estas reservas necesitas cambiar?",
        "¿Cuál es la reserva que quieres modificar?",
        "¿En cuál de estas reservas hacemos el cambio?"
    });

    /// <summary>
    /// Asking what field to modify.
    /// </summary>
    public static string ModificationSelectField() => Pick(new[]
    {
        "¿Qué quieres modificar de tu reserva?",
        "¿Qué te gustaría cambiar?",
        "Dime qué quieres modificar.",
        "¿Qué necesitas cambiar de la reserva?",
        "¿Qué parte de la reserva modificamos?",
        "¿Qué cambio necesitas hacer?",
        "¿Qué te gustaría modificar de tu reserva?",
        "Cuéntame, ¿qué quieres cambiar?",
        "¿Qué aspecto de la reserva cambiamos?",
        "¿En qué te ayudo con la modificación?"
    });

    /// <summary>
    /// Asking for new date.
    /// </summary>
    public static string ModificationAskNewDate() => Pick(new[]
    {
        "¿Para qué nuevo día quieres la reserva?",
        "¿A qué fecha lo movemos?",
        "Dime la nueva fecha que prefieres.",
        "¿Para qué día lo cambiamos?",
        "¿Qué día os viene mejor?",
        "¿A qué fecha quieres cambiar?",
        "¿Para cuándo lo movemos?",
        "¿Qué nueva fecha te gustaría?",
        "Dime el nuevo día para la reserva.",
        "¿A qué fecha lo pasamos?"
    });

    /// <summary>
    /// Asking for new time.
    /// </summary>
    public static string ModificationAskNewTime() => Pick(new[]
    {
        "¿A qué nueva hora os va mejor?",
        "¿A qué hora lo cambiamos?",
        "Dime la nueva hora que prefieres.",
        "¿A qué hora te gustaría cambiarlo?",
        "¿Qué hora os viene mejor?",
        "¿A qué hora lo movemos?",
        "¿Para qué hora lo cambiamos?",
        "Dime la nueva hora para la reserva.",
        "¿A qué hora queréis venir?",
        "¿Qué nueva hora preferís?"
    });

    /// <summary>
    /// Asking for new party size.
    /// </summary>
    public static string ModificationAskNewPartySize() => Pick(new[]
    {
        "¿Cuántas personas seréis ahora?",
        "¿A cuántos comensales lo cambiamos?",
        "Dime el nuevo número de personas.",
        "¿Cuántos seréis finalmente?",
        "¿Para cuántas personas lo cambio?",
        "¿Cuántas personas vendréis?",
        "¿A cuántos comensales actualizamos?",
        "¿Cuántos seréis ahora?",
        "Dime cuántas personas vendréis.",
        "¿Para cuántos lo actualizo?"
    });

    /// <summary>
    /// Asking for rice change details.
    /// </summary>
    public static string ModificationAskNewRice() => Pick(new[]
    {
        "¿Qué cambio quieres hacer con el arroz? Puedes cambiar el tipo, las raciones, o cancelarlo.",
        "¿Qué modificación necesitas en el arroz? (tipo, raciones, o cancelar)",
        "Dime qué quieres cambiar del arroz: tipo, raciones, o cancelarlo.",
        "¿Qué ajuste hacemos con el arroz? ¿Tipo, raciones, o lo quitamos?",
        "¿Qué cambio necesitas en el arroz? Puedes modificar tipo, raciones, o cancelarlo.",
        "¿Cómo modificamos el arroz? (cambiar tipo, raciones, o cancelar)",
        "¿Qué hacemos con el arroz? ¿Cambio de tipo, raciones, o lo quitamos?",
        "Cuéntame qué cambio necesitas en el arroz.",
        "¿Qué quieres modificar del arroz? Tipo, raciones, o cancelarlo.",
        "¿Qué ajustamos del arroz? Puedes cambiar tipo, raciones, o quitarlo."
    });

    /// <summary>
    /// Date unavailable, suggesting alternatives.
    /// </summary>
    public static string ModificationDateUnavailable() => Pick(new[]
    {
        "Lo siento, esa fecha no está disponible para tu grupo. Te sugiero estas alternativas:",
        "Vaya, esa fecha está completa. Aquí tienes otras opciones:",
        "No hay disponibilidad para esa fecha. ¿Te viene bien alguna de estas?",
        "Esa fecha no tiene hueco disponible. Te propongo estas alternativas:",
        "Lo siento, esa fecha no está disponible. ¿Alguna de estas te viene bien?",
        "No queda disponibilidad para esa fecha. Prueba con alguna de estas:",
        "Vaya, no hay hueco para ese día. Te sugiero estas opciones:",
        "Esa fecha está llena. ¿Te interesa alguna de estas alternativas?",
        "No hay disponibilidad ese día. ¿Qué tal alguna de estas opciones?",
        "Lo siento, no hay sitio para esa fecha. Te ofrezco estas alternativas:"
    });

    /// <summary>
    /// Time unavailable, suggesting alternatives.
    /// </summary>
    public static string ModificationTimeUnavailable() => Pick(new[]
    {
        "Lo siento, esa hora no está disponible. Te sugiero estas alternativas:",
        "Vaya, a esa hora está completo. Aquí tienes otras opciones:",
        "No hay disponibilidad a esa hora. ¿Te viene bien alguna de estas?",
        "Esa hora no tiene hueco. Te propongo estas alternativas:",
        "Lo siento, esa hora no está disponible. ¿Alguna de estas te va bien?",
        "No queda disponibilidad a esa hora. Prueba con alguna de estas:",
        "Vaya, no hay hueco a esa hora. Te sugiero estas opciones:",
        "Esa hora está llena. ¿Te interesa alguna de estas alternativas?",
        "No hay disponibilidad a esa hora. ¿Qué tal alguna de estas opciones?",
        "Lo siento, no hay sitio a esa hora. Te ofrezco estas alternativas:"
    });

    /// <summary>
    /// Modification successful.
    /// </summary>
    public static string ModificationSuccess() => Pick(new[]
    {
        "¡Perfecto! Tu reserva ha sido modificada correctamente. ¡Nos vemos pronto!",
        "¡Listo! He actualizado tu reserva. ¡Te esperamos!",
        "¡Hecho! La modificación se ha guardado. ¡Hasta pronto!",
        "¡Genial! Tu reserva ya está actualizada. ¡Nos vemos!",
        "¡Perfecto! He aplicado los cambios a tu reserva. ¡Te esperamos!",
        "¡Todo listo! La reserva ha sido modificada. ¡Hasta pronto!",
        "¡Modificación completada! Tu reserva está actualizada. ¡Nos vemos!",
        "¡Hecho! Los cambios han sido aplicados. ¡Te esperamos!",
        "¡Perfecto! Tu reserva ya refleja los cambios. ¡Hasta pronto!",
        "¡Genial! La modificación se ha realizado correctamente. ¡Nos vemos!"
    });

    /// <summary>
    /// Modification cancelled by user.
    /// </summary>
    public static string ModificationCancelled() => Pick(new[]
    {
        "De acuerdo, no hago ningún cambio. Tu reserva sigue igual. ¿Te ayudo con algo más?",
        "Vale, cancelo la modificación. Tu reserva queda como estaba. ¿Necesitas algo más?",
        "Entendido, no modifico nada. ¿Te puedo ayudar con otra cosa?",
        "Ok, dejamos la reserva como está. ¿En qué más te puedo ayudar?",
        "Perfecto, no hago cambios. Tu reserva sigue igual. ¿Algo más?",
        "De acuerdo, no hacemos el cambio. ¿Te ayudo con algo más?",
        "Vale, cancelamos la modificación. ¿Necesitas algo más?",
        "Entendido, dejamos todo como estaba. ¿Te puedo ayudar en algo?",
        "Ok, no hago ningún cambio. ¿En qué más puedo ayudarte?",
        "Perfecto, la reserva queda igual. ¿Necesitas ayuda con algo más?"
    });

    /// <summary>
    /// Large group modification (>10 people) - sending contact card.
    /// </summary>
    public static string ModificationLargeGroupVCard() => Pick(new[]
    {
        "Para grupos de más de 10 personas, te paso el contacto de nuestro equipo de reservas. Ellos te ayudarán con el cambio.",
        "Los cambios para grupos grandes los gestiona nuestro equipo directamente. Te envío su contacto.",
        "Para más de 10 personas, te pongo en contacto con nuestro equipo de reservas. Te envío la tarjeta.",
        "Para grupos numerosos, nuestro equipo te atenderá mejor. Te paso su contacto.",
        "Los grupos de más de 10 personas los gestionamos de forma personalizada. Te envío el contacto.",
        "Para ese número de personas, te paso el contacto de nuestro equipo especializado.",
        "Para grupos grandes te atiende nuestro equipo de reservas. Aquí tienes su contacto.",
        "Para más de 10 personas, mejor contacta directamente con nuestro equipo. Te paso el contacto.",
        "Los cambios para grupos numerosos los gestiona nuestro equipo. Te envío su tarjeta.",
        "Para grupos de este tamaño, te pongo en contacto con nuestro equipo de reservas."
    });

    /// <summary>
    /// Unsupported modification request (media/audio/complex) - continuing conversation.
    /// </summary>
    public static string ModificationUnsupportedRequest() => Pick(new[]
    {
        "Para este tipo de solicitud, te paso el contacto de nuestro equipo. Ellos te ayudarán mejor. ¿En qué más puedo ayudarte?",
        "Eso lo gestiona mejor nuestro equipo directamente. Te envío su contacto por si necesitas hablar con ellos. ¿Algo más?",
        "Para esa solicitud, te recomiendo contactar con nuestro equipo. Te paso su tarjeta. ¿Te ayudo con algo más?",
        "Eso escapa un poco de lo que puedo hacer yo. Te paso el contacto de nuestro equipo. ¿Necesitas algo más?",
        "Para eso es mejor que hables con nuestro equipo. Te envío su contacto. ¿En qué más te puedo ayudar?",
        "Esa solicitud la gestiona mejor nuestro equipo. Aquí tienes su contacto. ¿Algo más en lo que pueda ayudarte?",
        "Para eso te paso el contacto de nuestro equipo de reservas. ¿Necesitas ayuda con algo más?",
        "Eso lo resuelve mejor nuestro equipo directamente. Te paso su tarjeta. ¿Te ayudo con otra cosa?",
        "Para solicitudes así, te pongo en contacto con nuestro equipo. ¿En qué más puedo ayudarte?",
        "Eso es mejor gestionarlo con nuestro equipo. Te envío su contacto. ¿Algo más?"
    });

    /// <summary>
    /// Confirmation prompt for modification changes.
    /// </summary>
    public static string ModificationConfirmChanges() => Pick(new[]
    {
        "¿Confirmas este cambio?",
        "¿Te parece bien? Confirma con 'sí' para aplicar el cambio.",
        "¿Confirmamos el cambio?",
        "¿Está todo correcto? Di 'sí' para confirmar.",
        "¿Lo confirmamos?",
        "¿Procedo con el cambio?",
        "¿Confirmamos esta modificación?",
        "Si está todo bien, confirma con 'sí'.",
        "¿Aplicamos el cambio?",
        "¿Confirmas la modificación?"
    });

    /// <summary>
    /// Asking for number of tronas in modification.
    /// </summary>
    public static string ModificationAskTronas() => Pick(new[]
    {
        "¿Cuántas tronas necesitáis ahora?",
        "¿A cuántas tronas lo cambio?",
        "Dime cuántas tronas necesitáis.",
        "¿Cuántas tronas queréis?",
        "¿Cuántas tronas os preparo?",
        "¿A cuántas tronas actualizamos?",
        "Dime el nuevo número de tronas.",
        "¿Cuántas tronas van a ser?",
        "¿Para cuántas tronas lo cambio?",
        "¿Cuántas tronas necesitáis finalmente?"
    });

    /// <summary>
    /// Asking for number of carritos in modification.
    /// </summary>
    public static string ModificationAskCarritos() => Pick(new[]
    {
        "¿Cuántos carritos vais a traer ahora?",
        "¿A cuántos carritos lo cambio?",
        "Dime cuántos carritos traeréis.",
        "¿Cuántos carritos necesitáis?",
        "¿Cuántos carritos os reservo sitio?",
        "¿A cuántos carritos actualizamos?",
        "Dime el nuevo número de carritos.",
        "¿Cuántos carritos van a ser?",
        "¿Para cuántos carritos lo cambio?",
        "¿Cuántos carritos traeréis finalmente?"
    });

    private static string Pick(string[] options) =>
        options[_random.Next(options.Length)];
}
