package omnilingua.java;

import java.lang.annotation.*;

/** Déclare un contrat consommé par une classe de plugin. */
@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.TYPE)
@Repeatable(PluginImport.List.class)
public @interface PluginImport {
    Class<?> value();
    String name() default "";
    int version() default 1;
    boolean optional() default false;

    /** Regroupe plusieurs déclarations sur le même plugin. */
    @Retention(RetentionPolicy.RUNTIME)
    @Target(ElementType.TYPE)
    @interface List { PluginImport[] value(); }
}
